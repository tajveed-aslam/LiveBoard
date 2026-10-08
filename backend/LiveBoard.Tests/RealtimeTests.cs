using LiveBoard.Api.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;

namespace LiveBoard.Tests;

/// <summary>Two (or more) real SignalR clients on one board: changes made by one must reach the others.</summary>
public class RealtimeTests(TestApp app) : IClassFixture<TestApp>
{
    private sealed record Session(TestUser User, HubConnection Hub, EventRecorder Events) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Hub.DisposeAsync();
    }

    private async Task<Session> ConnectAsync(TestUser user, Guid boardId)
    {
        var hub = app.CreateHubConnection(user.Auth.Token);
        var events = new EventRecorder(hub);
        await hub.StartAsync();
        await hub.InvokeAsync<IReadOnlyList<PresenceUser>>("JoinBoard", boardId);
        return new Session(user, hub, events);
    }

    private async Task<(BoardDto Board, Session Alice, Session Bob)> TwoUsersOnOneBoardAsync()
    {
        var alice = await app.NewGuestAsync();
        var bob = await app.NewGuestAsync();
        var board = await alice.CreateBoardAsync();
        await bob.JoinAsync(board.ShareToken);
        return (board, await ConnectAsync(alice, board.Id), await ConnectAsync(bob, board.Id));
    }

    [Fact]
    public async Task A_card_created_by_one_user_appears_for_the_other()
    {
        var (board, alice, bob) = await TwoUsersOnOneBoardAsync();
        await using var _ = alice;
        await using var __ = bob;

        var card = await alice.User.CreateCardAsync(board.Id, board.Columns[0].Id, "Ship it");

        var received = await bob.Events.WaitForAsync<CardEvent>(BoardEvents.CardCreated);
        Assert.Equal((card.Id, "Ship it", board.Columns[0].Id), (received.Card.Id, received.Card.Title, received.Card.ColumnId));
        Assert.Equal(alice.User.Auth.DisplayName, received.Actor.DisplayName);
    }

    [Fact]
    public async Task A_move_broadcasts_the_final_order_that_matches_the_saved_board()
    {
        var (board, alice, bob) = await TwoUsersOnOneBoardAsync();
        await using var _ = alice;
        await using var __ = bob;
        var (todo, doing) = (board.Columns[0].Id, board.Columns[1].Id);
        var first = await alice.User.CreateCardAsync(board.Id, todo, "First");
        var second = await alice.User.CreateCardAsync(board.Id, todo, "Second");

        await bob.User.MoveCardAsync(board.Id, first.Id, doing, 0);

        var moved = await alice.Events.WaitForAsync<CardMovedEvent>(BoardEvents.CardMoved);
        Assert.Equal(todo, moved.FromColumnId);
        Assert.Equal([second.Id], moved.ColumnOrders[todo]);
        Assert.Equal([first.Id], moved.ColumnOrders[doing]);

        // What the event said is exactly what a fresh load of the board shows.
        var saved = await alice.User.GetBoardAsync(board.Id);
        Assert.Equal(moved.ColumnOrders[todo], saved.Columns[0].Cards.Select(c => c.Id));
        Assert.Equal(moved.ColumnOrders[doing], saved.Columns[1].Cards.Select(c => c.Id));
    }

    [Fact]
    public async Task Edits_column_changes_and_deletes_are_all_broadcast()
    {
        var (board, alice, bob) = await TwoUsersOnOneBoardAsync();
        await using var _ = alice;
        await using var __ = bob;
        var card = await alice.User.CreateCardAsync(board.Id, board.Columns[0].Id, "Draft");

        await alice.User.SendAsync<CardDto>(HttpMethod.Patch, $"/api/boards/{board.Id}/cards/{card.Id}", new { title = "Final", color = "red" });
        await alice.User.SendAsync<ColumnDto>(HttpMethod.Post, $"/api/boards/{board.Id}/columns", new { title = "Review" });
        await alice.User.RawAsync(HttpMethod.Patch, $"/api/boards/{board.Id}", new { title = "Renamed board" });
        await alice.User.RawAsync(HttpMethod.Delete, $"/api/boards/{board.Id}/cards/{card.Id}");

        Assert.Equal("Final", (await bob.Events.WaitForAsync<CardEvent>(BoardEvents.CardUpdated)).Card.Title);
        Assert.Equal("Review", (await bob.Events.WaitForAsync<ColumnEvent>(BoardEvents.ColumnCreated)).Column.Title);
        Assert.Equal("Renamed board", (await bob.Events.WaitForAsync<BoardUpdatedEvent>(BoardEvents.BoardUpdated)).Title);
        Assert.Equal(card.Id, (await bob.Events.WaitForAsync<CardDeletedEvent>(BoardEvents.CardDeleted)).CardId);
    }

    [Fact]
    public async Task Presence_tracks_who_joins_and_leaves()
    {
        var alice = await app.NewGuestAsync();
        var bob = await app.NewGuestAsync();
        var board = await alice.CreateBoardAsync();
        await bob.JoinAsync(board.ShareToken);
        await using var aliceSession = await ConnectAsync(alice, board.Id);

        var bobSession = await ConnectAsync(bob, board.Id);
        await aliceSession.Events.WaitForAsync<PresenceChangedEvent>(BoardEvents.PresenceChanged, e => e.Users.Count == 2);

        await bobSession.DisposeAsync();
        var afterLeave = await aliceSession.Events.WaitForAsync<PresenceChangedEvent>(
            BoardEvents.PresenceChanged, e => e.Users.Count == 1 && e.Users[0].UserId == alice.Id);
        Assert.Equal(alice.Auth.DisplayName, afterLeave.Users[0].DisplayName);
    }

    [Fact]
    public async Task Editing_indicator_is_shared_and_cleared_on_disconnect()
    {
        var (board, alice, bob) = await TwoUsersOnOneBoardAsync();
        await using var _ = bob;
        var card = await alice.User.CreateCardAsync(board.Id, board.Columns[0].Id, "Busy card");

        await alice.Hub.InvokeAsync("SetEditing", card.Id);
        var editing = await bob.Events.WaitForAsync<EditingChangedEvent>(BoardEvents.EditingChanged, e => e.CardId == card.Id);
        Assert.Equal(alice.User.Id, editing.UserId);

        await alice.DisposeAsync(); // closes the tab mid-edit
        await bob.Events.WaitForAsync<EditingChangedEvent>(BoardEvents.EditingChanged, e => e.UserId == alice.User.Id && e.CardId == null);
    }

    [Fact]
    public async Task Non_members_cannot_join_a_boards_live_channel()
    {
        var owner = await app.NewGuestAsync();
        var outsider = await app.NewGuestAsync();
        var board = await owner.CreateBoardAsync();

        await using var hub = app.CreateHubConnection(outsider.Auth.Token);
        await hub.StartAsync();
        var error = await Assert.ThrowsAsync<HubException>(() => hub.InvokeAsync<IReadOnlyList<PresenceUser>>("JoinBoard", board.Id));
        Assert.Contains("not found", error.Message);
    }

    [Fact]
    public async Task Members_are_told_when_someone_joins_and_when_the_board_is_deleted()
    {
        var owner = await app.NewGuestAsync();
        var board = await owner.CreateBoardAsync();
        await using var ownerSession = await ConnectAsync(owner, board.Id);

        var newcomer = await app.NewGuestAsync();
        await newcomer.JoinAsync(board.ShareToken);
        var joined = await ownerSession.Events.WaitForAsync<MemberJoinedEvent>(BoardEvents.MemberJoined);
        Assert.Equal(newcomer.Auth.DisplayName, joined.Member.DisplayName);

        await using var newcomerSession = await ConnectAsync(newcomer, board.Id);
        await owner.RawAsync(HttpMethod.Delete, $"/api/boards/{board.Id}");
        await newcomerSession.Events.WaitForAsync<BoardDeletedEvent>(BoardEvents.BoardDeleted);
    }
}
