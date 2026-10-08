using LiveBoard.Api.Data;
using LiveBoard.Api.Infrastructure;
using LiveBoard.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace LiveBoard.Api.Hubs;

/// <summary>
/// Real-time channel. Clients join a board's group to receive its change events (sent by the REST layer via
/// IHubContext after each write is saved) and to share presence. Writes deliberately don't go through the hub:
/// REST gives validation, status codes and rate limiting for free, and the hub stays a broadcast channel.
/// </summary>
[Authorize]
public sealed class BoardHub(AppDbContext db, PresenceTracker presence) : Hub
{
    public const string Path = "/hubs/board";

    public static string Group(Guid boardId) => $"board:{boardId}";

    /// <summary>Starts receiving a board's events. Returns who is currently online on it.</summary>
    public async Task<IReadOnlyList<PresenceUser>> JoinBoard(Guid boardId)
    {
        var user = Context.User!;
        var userId = user.GetUserId();
        var isMember = await db.BoardMembers.AnyAsync(m => m.BoardId == boardId && m.UserId == userId);
        if (!isMember)
            throw new HubException("Board not found.");

        await LeaveCurrentAsync();
        await Groups.AddToGroupAsync(Context.ConnectionId, Group(boardId));
        var users = presence.Join(Context.ConnectionId, boardId, userId, user.GetDisplayName(), user.IsGuest());
        await Clients.Group(Group(boardId)).SendAsync(BoardEvents.PresenceChanged, new PresenceChangedEvent(boardId, users));
        return users;
    }

    public Task LeaveBoard() => LeaveCurrentAsync();

    /// <summary>Tells collaborators this user opened (cardId) or closed (null) a card's editor.</summary>
    public async Task SetEditing(Guid? cardId)
    {
        var connection = presence.SetEditing(Context.ConnectionId, cardId);
        if (connection is null)
            return;
        await Clients.OthersInGroup(Group(connection.BoardId)).SendAsync(
            BoardEvents.EditingChanged,
            new EditingChangedEvent(connection.BoardId, connection.UserId, connection.DisplayName, cardId));
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await LeaveCurrentAsync();
        await base.OnDisconnectedAsync(exception);
    }

    private async Task LeaveCurrentAsync()
    {
        var result = presence.Leave(Context.ConnectionId);
        if (result is null)
            return;

        var (left, remaining) = result.Value;
        var group = Group(left.BoardId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, group);
        if (left.EditingCardId is not null)
            await Clients.Group(group).SendAsync(
                BoardEvents.EditingChanged, new EditingChangedEvent(left.BoardId, left.UserId, left.DisplayName, null));
        await Clients.Group(group).SendAsync(BoardEvents.PresenceChanged, new PresenceChangedEvent(left.BoardId, remaining));
    }
}
