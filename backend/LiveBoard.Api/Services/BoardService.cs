using System.Security.Cryptography;
using LiveBoard.Api.Data;
using LiveBoard.Api.Hubs;
using LiveBoard.Api.Infrastructure;
using LiveBoard.Api.Models;
using LiveBoard.Api.Options;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LiveBoard.Api.Services;

/// <summary>
/// Every board mutation goes through here: membership check → per-board lock → apply + save → broadcast the
/// resulting state to the board's SignalR group. Broadcasting after the save means clients only ever see
/// changes that were actually persisted.
/// </summary>
public sealed class BoardService(AppDbContext db, BoardLocks locks, IHubContext<BoardHub> hub, IOptions<BoardLimits> limits)
{
    public static readonly IReadOnlySet<string> Colors =
        new HashSet<string>(["red", "orange", "yellow", "green", "blue", "purple", "pink", "gray"]);

    private static readonly string[] DefaultColumns = ["To do", "In progress", "Done"];

    // ---------- Queries ----------

    public async Task<IReadOnlyList<BoardSummaryDto>> ListAsync(Guid userId, CancellationToken ct) =>
        await (from m in db.BoardMembers
               join b in db.Boards on m.BoardId equals b.Id
               where m.UserId == userId
               orderby b.UpdatedAt descending
               select new BoardSummaryDto(
                   b.Id,
                   b.Title,
                   m.Role,
                   db.BoardMembers.Count(x => x.BoardId == b.Id),
                   db.Cards.Count(c => c.BoardId == b.Id),
                   b.UpdatedAt))
            .ToListAsync(ct);

    public async Task<BoardDto> GetAsync(Guid boardId, Guid userId, CancellationToken ct)
    {
        var role = await RequireMemberAsync(boardId, userId, ct);
        var board = await db.Boards.AsNoTracking().SingleAsync(b => b.Id == boardId, ct);
        var columns = await db.Columns.AsNoTracking()
            .Where(c => c.BoardId == boardId)
            .OrderBy(c => c.Position)
            .ToListAsync(ct);
        var cards = await db.Cards.AsNoTracking()
            .Where(c => c.BoardId == boardId)
            .OrderBy(c => c.Position)
            .ToListAsync(ct);
        var members = await db.BoardMembers.AsNoTracking()
            .Where(m => m.BoardId == boardId)
            .Include(m => m.User)
            .ToListAsync(ct);

        var cardsByColumn = cards.ToLookup(c => c.ColumnId);
        return new BoardDto(
            board.Id,
            board.Title,
            board.ShareToken,
            role,
            columns.Select(c => ToDto(c, cardsByColumn[c.Id])).ToList(),
            members
                .OrderBy(m => m.Role)
                .ThenBy(m => m.User!.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(m => new MemberDto(m.UserId, m.User!.DisplayName, m.Role, m.User.IsGuest))
                .ToList(),
            board.UpdatedAt);
    }

    // ---------- Boards ----------

    public async Task<BoardDto> CreateAsync(Guid userId, bool isGuest, string title, bool withDefaultColumns, CancellationToken ct)
    {
        var limit = isGuest ? limits.Value.MaxBoardsPerGuest : limits.Value.MaxBoardsPerUser;
        if (await db.Boards.CountAsync(b => b.OwnerId == userId, ct) >= limit)
            throw new InputValidationException($"You can own at most {limit} boards. Delete one to create another.");

        var board = NewBoard(userId, CleanTitle(title));
        if (withDefaultColumns)
            board.Columns.AddRange(DefaultColumns.Select((t, i) => NewColumn(board.Id, t, i)));

        db.Boards.Add(board);
        await db.SaveChangesAsync(ct);
        return await GetAsync(board.Id, userId, ct);
    }

    /// <summary>A ready-made board for guests that explains how to try the real-time features.</summary>
    public async Task<Guid> CreateSampleBoardAsync(Guid userId, CancellationToken ct)
    {
        var board = NewBoard(userId, "Welcome to LiveBoard");
        var todo = NewColumn(board.Id, "To do", 0);
        var doing = NewColumn(board.Id, "In progress", 1);
        var done = NewColumn(board.Id, "Done", 2);
        board.Columns.AddRange([todo, doing, done]);

        AddCards(board.Id, todo,
            ("Open this board in a second window", "Click Share, copy the link and open it in another browser or a private window. Every change you make here appears there instantly.", "blue"),
            ("Drag me to In progress", "Grab a card and drop it in any column. Everyone on the board sees it move.", null),
            ("Add a card of your own", "Use \"Add a card\" at the bottom of any column.", null));
        AddCards(board.Id, doing,
            ("Rename a column", "Click a column title to edit it.", null),
            ("Give a card a colour label", "Open a card and pick a label colour.", "purple"));
        AddCards(board.Id, done,
            ("Start a guest session", "That happened when you clicked \"Try the live demo\".", "green"));

        db.Boards.Add(board);
        await db.SaveChangesAsync(ct);
        return board.Id;
    }

    public async Task RenameBoardAsync(Guid boardId, Actor actor, string title, CancellationToken ct)
    {
        using var _ = await locks.AcquireAsync(boardId, ct);
        await RequireMemberAsync(boardId, actor.UserId, ct);
        var board = await db.Boards.SingleAsync(b => b.Id == boardId, ct);
        board.Title = CleanTitle(title);
        board.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await BroadcastAsync(boardId, BoardEvents.BoardUpdated, new BoardUpdatedEvent(boardId, board.Title, actor));
    }

    public async Task DeleteBoardAsync(Guid boardId, Actor actor, CancellationToken ct)
    {
        using var _ = await locks.AcquireAsync(boardId, ct);
        await RequireOwnerAsync(boardId, actor.UserId, ct);
        var board = await db.Boards.SingleAsync(b => b.Id == boardId, ct);
        db.Boards.Remove(board);
        await db.SaveChangesAsync(ct);
        await BroadcastAsync(boardId, BoardEvents.BoardDeleted, new BoardDeletedEvent(boardId, actor));
    }

    /// <summary>New share link; anyone holding the old one can no longer join (existing members stay).</summary>
    public async Task<string> RegenerateShareTokenAsync(Guid boardId, Guid userId, CancellationToken ct)
    {
        using var _ = await locks.AcquireAsync(boardId, ct);
        await RequireOwnerAsync(boardId, userId, ct);
        var board = await db.Boards.SingleAsync(b => b.Id == boardId, ct);
        board.ShareToken = NewShareToken();
        await db.SaveChangesAsync(ct);
        return board.ShareToken;
    }

    public async Task<JoinResultDto> JoinAsync(string shareToken, Actor actor, bool isGuest, CancellationToken ct)
    {
        var board = await db.Boards.AsNoTracking().SingleOrDefaultAsync(b => b.ShareToken == shareToken, ct)
            ?? throw new NotFoundException("This share link is invalid or has been reset by the board owner.");

        using var _ = await locks.AcquireAsync(board.Id, ct);
        if (await db.BoardMembers.AnyAsync(m => m.BoardId == board.Id && m.UserId == actor.UserId, ct))
            return new JoinResultDto(board.Id, AlreadyMember: true);

        db.BoardMembers.Add(new BoardMember
        {
            BoardId = board.Id,
            UserId = actor.UserId,
            Role = BoardRole.Editor,
            JoinedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);
        await BroadcastAsync(board.Id, BoardEvents.MemberJoined,
            new MemberJoinedEvent(board.Id, new MemberDto(actor.UserId, actor.DisplayName, BoardRole.Editor, isGuest)));
        return new JoinResultDto(board.Id, AlreadyMember: false);
    }

    public async Task LeaveAsync(Guid boardId, Guid userId, CancellationToken ct)
    {
        using var _ = await locks.AcquireAsync(boardId, ct);
        var role = await RequireMemberAsync(boardId, userId, ct);
        if (role == BoardRole.Owner)
            throw new InputValidationException("The owner can't leave their own board. Delete it instead.");
        await db.BoardMembers.Where(m => m.BoardId == boardId && m.UserId == userId).ExecuteDeleteAsync(ct);
    }

    // ---------- Columns ----------

    public async Task<ColumnDto> CreateColumnAsync(Guid boardId, Actor actor, string title, CancellationToken ct)
    {
        using var _ = await locks.AcquireAsync(boardId, ct);
        await RequireMemberAsync(boardId, actor.UserId, ct);
        var count = await db.Columns.CountAsync(c => c.BoardId == boardId, ct);
        if (count >= limits.Value.MaxColumnsPerBoard)
            throw new InputValidationException($"A board can have at most {limits.Value.MaxColumnsPerBoard} columns.");

        var column = NewColumn(boardId, CleanTitle(title), count);
        db.Columns.Add(column);
        await TouchBoardAsync(boardId, ct);
        await db.SaveChangesAsync(ct);

        var dto = ToDto(column, []);
        await BroadcastAsync(boardId, BoardEvents.ColumnCreated, new ColumnEvent(boardId, dto, actor));
        return dto;
    }

    public async Task<ColumnDto> RenameColumnAsync(Guid boardId, Guid columnId, Actor actor, string title, CancellationToken ct)
    {
        using var _ = await locks.AcquireAsync(boardId, ct);
        await RequireMemberAsync(boardId, actor.UserId, ct);
        var column = await FindColumnAsync(boardId, columnId, ct);
        column.Title = CleanTitle(title);
        await TouchBoardAsync(boardId, ct);
        await db.SaveChangesAsync(ct);

        var cards = await db.Cards.AsNoTracking().Where(c => c.ColumnId == columnId).OrderBy(c => c.Position).ToListAsync(ct);
        var dto = ToDto(column, cards);
        await BroadcastAsync(boardId, BoardEvents.ColumnUpdated, new ColumnEvent(boardId, dto, actor));
        return dto;
    }

    public async Task DeleteColumnAsync(Guid boardId, Guid columnId, Actor actor, CancellationToken ct)
    {
        using var _ = await locks.AcquireAsync(boardId, ct);
        await RequireMemberAsync(boardId, actor.UserId, ct);
        var columns = await db.Columns.Where(c => c.BoardId == boardId).OrderBy(c => c.Position).ToListAsync(ct);
        var column = columns.SingleOrDefault(c => c.Id == columnId) ?? throw new NotFoundException("Column not found.");

        // Cards are removed explicitly (rather than relying on the DB cascade) so the tracked graph stays
        // consistent; the transaction makes the card delete and the column delete all-or-nothing.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Cards.Where(c => c.ColumnId == columnId).ExecuteDeleteAsync(ct);
        db.Columns.Remove(column);
        Renumber(columns.Where(c => c.Id != columnId).ToList());
        await TouchBoardAsync(boardId, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await BroadcastAsync(boardId, BoardEvents.ColumnDeleted, new ColumnDeletedEvent(boardId, columnId, column.Title, actor));
    }

    public async Task<IReadOnlyList<Guid>> MoveColumnAsync(Guid boardId, Guid columnId, Actor actor, int toIndex, CancellationToken ct)
    {
        using var _ = await locks.AcquireAsync(boardId, ct);
        await RequireMemberAsync(boardId, actor.UserId, ct);
        var columns = await db.Columns.Where(c => c.BoardId == boardId).OrderBy(c => c.Position).ToListAsync(ct);
        var column = columns.SingleOrDefault(c => c.Id == columnId) ?? throw new NotFoundException("Column not found.");

        var ordered = Ordering.Move(columns, column, toIndex);
        Renumber(ordered);
        await TouchBoardAsync(boardId, ct);
        await db.SaveChangesAsync(ct);

        var ids = ordered.Select(c => c.Id).ToList();
        await BroadcastAsync(boardId, BoardEvents.ColumnsReordered, new ColumnsReorderedEvent(boardId, ids, actor));
        return ids;
    }

    // ---------- Cards ----------

    public async Task<CardDto> CreateCardAsync(Guid boardId, Actor actor, CreateCardRequest request, CancellationToken ct)
    {
        using var _ = await locks.AcquireAsync(boardId, ct);
        await RequireMemberAsync(boardId, actor.UserId, ct);
        await FindColumnAsync(boardId, request.ColumnId, ct, notFoundAsBadRequest: true);
        if (await db.Cards.CountAsync(c => c.BoardId == boardId, ct) >= limits.Value.MaxCardsPerBoard)
            throw new InputValidationException($"A board can have at most {limits.Value.MaxCardsPerBoard} cards.");

        var now = DateTime.UtcNow;
        var card = new Card
        {
            Id = Guid.NewGuid(),
            BoardId = boardId,
            ColumnId = request.ColumnId,
            Title = CleanTitle(request.Title),
            Description = request.Description?.Trim() ?? "",
            Position = await db.Cards.CountAsync(c => c.ColumnId == request.ColumnId, ct), // append at the bottom
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Cards.Add(card);
        await TouchBoardAsync(boardId, ct);
        await db.SaveChangesAsync(ct);

        var dto = ToDto(card);
        await BroadcastAsync(boardId, BoardEvents.CardCreated, new CardEvent(boardId, dto, actor));
        return dto;
    }

    public async Task<CardDto> UpdateCardAsync(Guid boardId, Guid cardId, Actor actor, UpdateCardRequest request, CancellationToken ct)
    {
        using var _ = await locks.AcquireAsync(boardId, ct);
        await RequireMemberAsync(boardId, actor.UserId, ct);
        var card = await FindCardAsync(boardId, cardId, ct);

        if (request.Title is not null)
            card.Title = CleanTitle(request.Title);
        if (request.Description is not null)
            card.Description = request.Description.Trim();
        if (request.Color is not null)
        {
            var color = request.Color.Trim().ToLowerInvariant();
            if (color.Length > 0 && !Colors.Contains(color))
                throw new InputValidationException($"Unknown label colour. Use one of: {string.Join(", ", Colors)}.");
            card.Color = color.Length == 0 ? null : color;
        }
        card.UpdatedAt = DateTime.UtcNow;
        await TouchBoardAsync(boardId, ct);
        await db.SaveChangesAsync(ct);

        var dto = ToDto(card);
        await BroadcastAsync(boardId, BoardEvents.CardUpdated, new CardEvent(boardId, dto, actor));
        return dto;
    }

    public async Task DeleteCardAsync(Guid boardId, Guid cardId, Actor actor, CancellationToken ct)
    {
        using var _ = await locks.AcquireAsync(boardId, ct);
        await RequireMemberAsync(boardId, actor.UserId, ct);
        var card = await FindCardAsync(boardId, cardId, ct);
        var siblings = await db.Cards.Where(c => c.ColumnId == card.ColumnId && c.Id != cardId).OrderBy(c => c.Position).ToListAsync(ct);

        db.Cards.Remove(card);
        Renumber(siblings);
        await TouchBoardAsync(boardId, ct);
        await db.SaveChangesAsync(ct);

        await BroadcastAsync(boardId, BoardEvents.CardDeleted, new CardDeletedEvent(boardId, cardId, card.ColumnId, card.Title, actor));
    }

    /// <summary>
    /// Moves a card to <c>ToIndex</c> in <c>ToColumnId</c> (same or another column). The broadcast carries the
    /// complete final order of every column touched, so concurrent moves converge on every client.
    /// </summary>
    public async Task<CardMovedEvent> MoveCardAsync(Guid boardId, Guid cardId, Actor actor, MoveCardRequest request, CancellationToken ct)
    {
        using var _ = await locks.AcquireAsync(boardId, ct);
        await RequireMemberAsync(boardId, actor.UserId, ct);
        var card = await FindCardAsync(boardId, cardId, ct);
        await FindColumnAsync(boardId, request.ToColumnId, ct, notFoundAsBadRequest: true);

        var fromColumnId = card.ColumnId;
        var orders = new Dictionary<Guid, IReadOnlyList<Guid>>();

        if (fromColumnId == request.ToColumnId)
        {
            var cards = await db.Cards.Where(c => c.ColumnId == fromColumnId).OrderBy(c => c.Position).ToListAsync(ct);
            var ordered = Ordering.Move(cards, card, request.ToIndex);
            Renumber(ordered);
            orders[fromColumnId] = ordered.Select(c => c.Id).ToList();
        }
        else
        {
            var source = await db.Cards.Where(c => c.ColumnId == fromColumnId && c.Id != cardId).OrderBy(c => c.Position).ToListAsync(ct);
            var target = await db.Cards.Where(c => c.ColumnId == request.ToColumnId).OrderBy(c => c.Position).ToListAsync(ct);
            var newTarget = Ordering.Insert(target, card, request.ToIndex);
            card.ColumnId = request.ToColumnId;
            Renumber(source);
            Renumber(newTarget);
            orders[fromColumnId] = source.Select(c => c.Id).ToList();
            orders[request.ToColumnId] = newTarget.Select(c => c.Id).ToList();
        }

        card.UpdatedAt = DateTime.UtcNow;
        await TouchBoardAsync(boardId, ct);
        await db.SaveChangesAsync(ct);

        var moved = new CardMovedEvent(boardId, ToDto(card), fromColumnId, orders, actor);
        await BroadcastAsync(boardId, BoardEvents.CardMoved, moved);
        return moved;
    }

    // ---------- Helpers ----------

    private async Task<BoardRole> RequireMemberAsync(Guid boardId, Guid userId, CancellationToken ct)
    {
        var member = await db.BoardMembers.AsNoTracking()
            .SingleOrDefaultAsync(m => m.BoardId == boardId && m.UserId == userId, ct);
        return member?.Role ?? throw new NotFoundException("Board not found.");
    }

    private async Task RequireOwnerAsync(Guid boardId, Guid userId, CancellationToken ct)
    {
        if (await RequireMemberAsync(boardId, userId, ct) != BoardRole.Owner)
            throw new ForbiddenException("Only the board's owner can do that.");
    }

    private async Task<BoardColumn> FindColumnAsync(Guid boardId, Guid columnId, CancellationToken ct, bool notFoundAsBadRequest = false)
    {
        var column = await db.Columns.SingleOrDefaultAsync(c => c.Id == columnId && c.BoardId == boardId, ct);
        if (column is not null)
            return column;
        // A missing target column in a create/move body is a bad request, not a missing URL resource.
        throw notFoundAsBadRequest
            ? new InputValidationException("That column doesn't exist on this board (it may have just been deleted).")
            : new NotFoundException("Column not found.");
    }

    private async Task<Card> FindCardAsync(Guid boardId, Guid cardId, CancellationToken ct) =>
        await db.Cards.SingleOrDefaultAsync(c => c.Id == cardId && c.BoardId == boardId, ct)
            ?? throw new NotFoundException("Card not found (it may have just been deleted).");

    private async Task TouchBoardAsync(Guid boardId, CancellationToken ct)
    {
        var board = await db.Boards.SingleAsync(b => b.Id == boardId, ct);
        board.UpdatedAt = DateTime.UtcNow;
    }

    // Broadcast with no cancellation: the change is already saved, so collaborators must hear about it even if
    // the caller's request was aborted meanwhile.
    private Task BroadcastAsync(Guid boardId, string eventName, object payload) =>
        hub.Clients.Group(BoardHub.Group(boardId)).SendAsync(eventName, payload, CancellationToken.None);

    private static void Renumber(IEnumerable<BoardColumn> columns)
    {
        var i = 0;
        foreach (var column in columns)
            column.Position = i++;
    }

    private static void Renumber(IEnumerable<Card> cards)
    {
        var i = 0;
        foreach (var card in cards)
            card.Position = i++;
    }

    private static string CleanTitle(string? title)
    {
        var clean = title?.Trim() ?? "";
        return clean.Length > 0 ? clean : throw new InputValidationException("Title can't be empty.");
    }

    private static string NewShareToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private static Board NewBoard(Guid ownerId, string title)
    {
        var now = DateTime.UtcNow;
        var board = new Board
        {
            Id = Guid.NewGuid(),
            Title = title,
            OwnerId = ownerId,
            ShareToken = NewShareToken(),
            CreatedAt = now,
            UpdatedAt = now,
        };
        board.Members.Add(new BoardMember { BoardId = board.Id, UserId = ownerId, Role = BoardRole.Owner, JoinedAt = now });
        return board;
    }

    private static BoardColumn NewColumn(Guid boardId, string title, int position) => new()
    {
        Id = Guid.NewGuid(),
        BoardId = boardId,
        Title = title,
        Position = position,
        CreatedAt = DateTime.UtcNow,
    };

    private static void AddCards(Guid boardId, BoardColumn column, params (string Title, string Description, string? Color)[] cards)
    {
        var now = DateTime.UtcNow;
        column.Cards.AddRange(cards.Select((c, i) => new Card
        {
            Id = Guid.NewGuid(),
            BoardId = boardId,
            ColumnId = column.Id,
            Title = c.Title,
            Description = c.Description,
            Color = c.Color,
            Position = i,
            CreatedAt = now,
            UpdatedAt = now,
        }));
    }

    internal static CardDto ToDto(Card c) => new(c.Id, c.ColumnId, c.Title, c.Description, c.Color, c.Position, c.UpdatedAt);

    private static ColumnDto ToDto(BoardColumn c, IEnumerable<Card> cards) =>
        new(c.Id, c.Title, c.Position, cards.OrderBy(x => x.Position).Select(ToDto).ToList());
}
