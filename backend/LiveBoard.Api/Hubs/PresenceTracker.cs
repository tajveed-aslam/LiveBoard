using LiveBoard.Api.Models;

namespace LiveBoard.Api.Hubs;

/// <summary>
/// In-memory record of which SignalR connection is viewing which board. A user with two tabs open has two
/// connections but appears once in the online list. One board per connection; joining another board leaves
/// the previous one. (Single-instance assumption, as with <see cref="Services.BoardLocks"/>.)
/// </summary>
public sealed class PresenceTracker
{
    public sealed record Connection(Guid BoardId, Guid UserId, string DisplayName, bool IsGuest, Guid? EditingCardId);

    private readonly object _gate = new();
    private readonly Dictionary<string, Connection> _connections = new();

    public IReadOnlyList<PresenceUser> Join(string connectionId, Guid boardId, Guid userId, string displayName, bool isGuest)
    {
        lock (_gate)
        {
            _connections[connectionId] = new Connection(boardId, userId, displayName, isGuest, null);
            return UsersOnLocked(boardId);
        }
    }

    /// <summary>Removes the connection; returns what it was doing and who is left on that board.</summary>
    public (Connection Left, IReadOnlyList<PresenceUser> Remaining)? Leave(string connectionId)
    {
        lock (_gate)
        {
            if (!_connections.Remove(connectionId, out var left))
                return null;
            return (left, UsersOnLocked(left.BoardId));
        }
    }

    public Connection? Get(string connectionId)
    {
        lock (_gate)
            return _connections.GetValueOrDefault(connectionId);
    }

    public Connection? SetEditing(string connectionId, Guid? cardId)
    {
        lock (_gate)
        {
            if (!_connections.TryGetValue(connectionId, out var current))
                return null;
            return _connections[connectionId] = current with { EditingCardId = cardId };
        }
    }

    public IReadOnlyList<PresenceUser> UsersOn(Guid boardId)
    {
        lock (_gate)
            return UsersOnLocked(boardId);
    }

    private List<PresenceUser> UsersOnLocked(Guid boardId) =>
        _connections.Values
            .Where(c => c.BoardId == boardId)
            .GroupBy(c => c.UserId)
            .Select(g => new PresenceUser(g.Key, g.First().DisplayName, g.First().IsGuest, g.Count()))
            .OrderBy(u => u.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
