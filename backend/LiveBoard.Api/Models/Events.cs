namespace LiveBoard.Api.Models;

/// <summary>Who made a change; lets clients show "Sam moved X to Done".</summary>
public sealed record Actor(Guid UserId, string DisplayName);

/// <summary>
/// SignalR client method names. Every event carries the full resulting state of what changed (not a diff),
/// so applying it is idempotent and every client converges on the server's order even under concurrent edits.
/// </summary>
public static class BoardEvents
{
    public const string BoardUpdated     = "BoardUpdated";
    public const string BoardDeleted     = "BoardDeleted";
    public const string ColumnCreated    = "ColumnCreated";
    public const string ColumnUpdated    = "ColumnUpdated";
    public const string ColumnDeleted    = "ColumnDeleted";
    public const string ColumnsReordered = "ColumnsReordered";
    public const string CardCreated      = "CardCreated";
    public const string CardUpdated      = "CardUpdated";
    public const string CardDeleted      = "CardDeleted";
    public const string CardMoved        = "CardMoved";
    public const string MemberJoined     = "MemberJoined";
    public const string PresenceChanged  = "PresenceChanged";
    public const string EditingChanged   = "EditingChanged";
}

public sealed record BoardUpdatedEvent(Guid BoardId, string Title, Actor Actor);

public sealed record BoardDeletedEvent(Guid BoardId, Actor Actor);

public sealed record ColumnEvent(Guid BoardId, ColumnDto Column, Actor Actor);

public sealed record ColumnDeletedEvent(Guid BoardId, Guid ColumnId, string Title, Actor Actor);

public sealed record ColumnsReorderedEvent(Guid BoardId, IReadOnlyList<Guid> ColumnIds, Actor Actor);

public sealed record CardEvent(Guid BoardId, CardDto Card, Actor Actor);

public sealed record CardDeletedEvent(Guid BoardId, Guid CardId, Guid ColumnId, string Title, Actor Actor);

/// <summary>
/// <paramref name="ColumnOrders"/> holds the complete, final card order of every column the move touched.
/// </summary>
public sealed record CardMovedEvent(
    Guid BoardId,
    CardDto Card,
    Guid FromColumnId,
    IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> ColumnOrders,
    Actor Actor);

public sealed record MemberJoinedEvent(Guid BoardId, MemberDto Member);

public sealed record PresenceUser(Guid UserId, string DisplayName, bool IsGuest, int Connections);

public sealed record PresenceChangedEvent(Guid BoardId, IReadOnlyList<PresenceUser> Users);

/// <summary>A collaborator opened (CardId set) or closed (null) a card's editor.</summary>
public sealed record EditingChangedEvent(Guid BoardId, Guid UserId, string DisplayName, Guid? CardId);
