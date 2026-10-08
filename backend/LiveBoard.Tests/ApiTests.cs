using System.Net;
using LiveBoard.Api.Models;

namespace LiveBoard.Tests;

public class ApiTests(TestApp app) : IClassFixture<TestApp>
{
    [Fact]
    public async Task Guests_get_a_sample_board_that_explains_the_demo()
    {
        var guest = await app.NewGuestAsync(withSampleBoard: true);

        var boards = await guest.GetAsync<List<BoardSummaryDto>>("/api/boards");
        var summary = Assert.Single(boards);
        var board = await guest.GetBoardAsync(summary.Id);

        Assert.Equal(BoardRole.Owner, board.MyRole);
        Assert.Equal(["To do", "In progress", "Done"], board.Columns.Select(c => c.Title));
        Assert.Equal(6, board.Columns.Sum(c => c.Cards.Count));
        Assert.Contains(board.Columns[0].Cards, c => c.Title.Contains("second window"));
        Assert.Matches(@"^\w+ \w+$", guest.Auth.DisplayName);
    }

    [Fact]
    public async Task New_boards_start_with_three_columns_and_show_in_the_list_with_counts()
    {
        var user = await app.NewGuestAsync();
        var board = await user.CreateBoardAsync("Sprint 12");
        await user.CreateCardAsync(board.Id, board.Columns[0].Id, "First");

        var summary = Assert.Single(await user.GetAsync<List<BoardSummaryDto>>("/api/boards"));
        Assert.Equal(("Sprint 12", 1, 1), (summary.Title, summary.MemberCount, summary.CardCount));
        Assert.Equal(3, board.Columns.Count);
    }

    [Fact]
    public async Task Outsiders_get_404_and_the_share_link_grants_editor_access()
    {
        var owner = await app.NewGuestAsync();
        var outsider = await app.NewGuestAsync();
        var board = await owner.CreateBoardAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await outsider.RawAsync(HttpMethod.Get, $"/api/boards/{board.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.RawAsync(HttpMethod.Post, $"/api/boards/{board.Id}/cards",
            new { columnId = board.Columns[0].Id, title = "sneaky" })).StatusCode);

        var joined = await outsider.JoinAsync(board.ShareToken);
        Assert.False(joined.AlreadyMember);
        Assert.True((await outsider.JoinAsync(board.ShareToken)).AlreadyMember);

        var asEditor = await outsider.GetBoardAsync(board.Id);
        Assert.Equal(BoardRole.Editor, asEditor.MyRole);
        Assert.Equal(2, asEditor.Members.Count);
        Assert.Equal(BoardRole.Owner, asEditor.Members[0].Role);
    }

    [Fact]
    public async Task Resetting_the_share_link_blocks_new_joins_but_keeps_existing_members()
    {
        var owner = await app.NewGuestAsync();
        var member = await app.NewGuestAsync();
        var latecomer = await app.NewGuestAsync();
        var board = await owner.CreateBoardAsync();
        await member.JoinAsync(board.ShareToken);

        var fresh = await owner.SendAsync<ShareTokenDto>(HttpMethod.Post, $"/api/boards/{board.Id}/share-token");

        Assert.NotEqual(board.ShareToken, fresh.ShareToken);
        Assert.Equal(HttpStatusCode.NotFound, (await latecomer.RawAsync(HttpMethod.Post, $"/api/boards/join/{board.ShareToken}")).StatusCode);
        Assert.Equal(BoardRole.Editor, (await member.GetBoardAsync(board.Id)).MyRole);
        // Only the owner can reset it.
        Assert.Equal(HttpStatusCode.Forbidden, (await member.RawAsync(HttpMethod.Post, $"/api/boards/{board.Id}/share-token")).StatusCode);
    }

    [Fact]
    public async Task Only_the_owner_can_delete_and_the_owner_cannot_leave()
    {
        var owner = await app.NewGuestAsync();
        var editor = await app.NewGuestAsync();
        var board = await owner.CreateBoardAsync();
        await editor.JoinAsync(board.ShareToken);

        Assert.Equal(HttpStatusCode.Forbidden, (await editor.RawAsync(HttpMethod.Delete, $"/api/boards/{board.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.RawAsync(HttpMethod.Delete, $"/api/boards/{board.Id}/members/me")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await editor.RawAsync(HttpMethod.Delete, $"/api/boards/{board.Id}/members/me")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.RawAsync(HttpMethod.Delete, $"/api/boards/{board.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.RawAsync(HttpMethod.Get, $"/api/boards/{board.Id}")).StatusCode);
    }

    [Fact]
    public async Task Card_edits_validate_titles_and_label_colours()
    {
        var user = await app.NewGuestAsync();
        var board = await user.CreateBoardAsync();
        var card = await user.CreateCardAsync(board.Id, board.Columns[0].Id, "  Write tests  ");
        Assert.Equal("Write tests", card.Title);

        var updated = await user.SendAsync<CardDto>(HttpMethod.Patch, $"/api/boards/{board.Id}/cards/{card.Id}",
            new { description = "For the move endpoint", color = "Green" });
        Assert.Equal(("Write tests", "For the move endpoint", "green"), (updated.Title, updated.Description, updated.Color));

        var cleared = await user.SendAsync<CardDto>(HttpMethod.Patch, $"/api/boards/{board.Id}/cards/{card.Id}", new { color = "" });
        Assert.Null(cleared.Color);

        Assert.Equal(HttpStatusCode.BadRequest, (await user.RawAsync(HttpMethod.Patch, $"/api/boards/{board.Id}/cards/{card.Id}", new { color = "neon" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await user.RawAsync(HttpMethod.Patch, $"/api/boards/{board.Id}/cards/{card.Id}", new { title = "   " })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await user.RawAsync(HttpMethod.Post, $"/api/boards/{board.Id}/cards",
            new { columnId = Guid.NewGuid(), title = "nowhere" })).StatusCode);
    }

    [Fact]
    public async Task Moving_cards_keeps_positions_contiguous_within_and_across_columns()
    {
        var user = await app.NewGuestAsync();
        var board = await user.CreateBoardAsync();
        var (todo, done) = (board.Columns[0].Id, board.Columns[2].Id);
        var a = await user.CreateCardAsync(board.Id, todo, "A");
        var b = await user.CreateCardAsync(board.Id, todo, "B");
        var c = await user.CreateCardAsync(board.Id, todo, "C");

        // Within a column: C to the top.
        var within = await user.MoveCardAsync(board.Id, c.Id, todo, 0);
        Assert.Equal([c.Id, a.Id, b.Id], within.ColumnOrders[todo]);

        // Across columns: A into Done, past the end (clamped).
        var across = await user.MoveCardAsync(board.Id, a.Id, done, 99);
        Assert.Equal([c.Id, b.Id], across.ColumnOrders[todo]);
        Assert.Equal([a.Id], across.ColumnOrders[done]);
        Assert.Equal(todo, across.FromColumnId);

        var after = await user.GetBoardAsync(board.Id);
        AssertContiguous(after);
        Assert.Equal(["C", "B"], after.Columns[0].Cards.Select(x => x.Title));
        Assert.Equal(["A"], after.Columns[2].Cards.Select(x => x.Title));
    }

    [Fact]
    public async Task Deleting_a_column_removes_its_cards_and_renumbers_the_rest()
    {
        var user = await app.NewGuestAsync();
        var board = await user.CreateBoardAsync();
        await user.CreateCardAsync(board.Id, board.Columns[1].Id, "In progress card");

        Assert.Equal(HttpStatusCode.NoContent, (await user.RawAsync(HttpMethod.Delete, $"/api/boards/{board.Id}/columns/{board.Columns[1].Id}")).StatusCode);

        var after = await user.GetBoardAsync(board.Id);
        Assert.Equal(["To do", "Done"], after.Columns.Select(c => c.Title));
        Assert.Equal([0, 1], after.Columns.Select(c => c.Position));
        Assert.Equal(0, after.Columns.Sum(c => c.Cards.Count));
    }

    [Fact]
    public async Task Columns_can_be_reordered()
    {
        var user = await app.NewGuestAsync();
        var board = await user.CreateBoardAsync();

        var order = await user.SendAsync<List<Guid>>(HttpMethod.Post, $"/api/boards/{board.Id}/columns/{board.Columns[2].Id}/move", new { toIndex = 0 });

        Assert.Equal([board.Columns[2].Id, board.Columns[0].Id, board.Columns[1].Id], order);
        Assert.Equal(["Done", "To do", "In progress"], (await user.GetBoardAsync(board.Id)).Columns.Select(c => c.Title));
    }

    [Fact]
    public async Task Concurrent_moves_from_two_users_never_corrupt_the_order()
    {
        var owner = await app.NewGuestAsync();
        var collaborator = await app.NewGuestAsync();
        var board = await owner.CreateBoardAsync();
        await collaborator.JoinAsync(board.ShareToken);
        var columnIds = board.Columns.Select(c => c.Id).ToArray();
        var cards = new List<CardDto>();
        for (var i = 0; i < 12; i++)
            cards.Add(await owner.CreateCardAsync(board.Id, columnIds[i % 3], $"Card {i}"));

        // 40 random moves fired at once, split across both users.
        var random = new Random(42);
        var moves = Enumerable.Range(0, 40).Select(i =>
        {
            var who = i % 2 == 0 ? owner : collaborator;
            return who.MoveCardAsync(board.Id, cards[random.Next(cards.Count)].Id, columnIds[random.Next(3)], random.Next(6));
        });
        await Task.WhenAll(moves);

        var after = await owner.GetBoardAsync(board.Id);
        AssertContiguous(after);
        Assert.Equal(cards.Select(c => c.Id).Order(), after.Columns.SelectMany(c => c.Cards).Select(c => c.Id).Order());
    }

    private static void AssertContiguous(BoardDto board)
    {
        Assert.Equal(Enumerable.Range(0, board.Columns.Count), board.Columns.Select(c => c.Position));
        foreach (var column in board.Columns)
            Assert.Equal(Enumerable.Range(0, column.Cards.Count), column.Cards.Select(c => c.Position));
    }
}
