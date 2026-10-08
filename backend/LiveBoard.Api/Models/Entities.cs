namespace LiveBoard.Api.Models;

public enum BoardRole
{
    Owner,
    Editor,
}

public sealed class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    /// <summary>Shown to collaborators (presence, activity, "editing" badges).</summary>
    public string DisplayName { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    /// <summary>Temporary account created by the "Try the live demo" button.</summary>
    public bool IsGuest { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class Board
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public Guid OwnerId { get; set; }
    /// <summary>Secret for the share link. Regenerating it invalidates old links.</summary>
    public string ShareToken { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<BoardColumn> Columns { get; set; } = [];
    public List<BoardMember> Members { get; set; } = [];
}

public sealed class BoardMember
{
    public Guid BoardId { get; set; }
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public BoardRole Role { get; set; }
    public DateTime JoinedAt { get; set; }
}

public sealed class BoardColumn
{
    public Guid Id { get; set; }
    public Guid BoardId { get; set; }
    public string Title { get; set; } = "";
    /// <summary>0-based, contiguous within the board.</summary>
    public int Position { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<Card> Cards { get; set; } = [];
}

public sealed class Card
{
    public Guid Id { get; set; }
    /// <summary>Denormalised so board-wide queries and limits don't need a join.</summary>
    public Guid BoardId { get; set; }
    public Guid ColumnId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    /// <summary>Optional label colour key (e.g. "red", "green"); null for none.</summary>
    public string? Color { get; set; }
    /// <summary>0-based, contiguous within the column.</summary>
    public int Position { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
