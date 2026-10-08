using System.ComponentModel.DataAnnotations;

namespace LiveBoard.Api.Models;

// ---------- Auth ----------

public sealed record RegisterRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; init; } = "";

    [Required]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
    [MaxLength(128, ErrorMessage = "Password must be at most 128 characters.")]
    public string Password { get; init; } = "";

    [MaxLength(40, ErrorMessage = "Display name must be at most 40 characters.")]
    public string? DisplayName { get; init; }
}

public sealed record LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; init; } = "";

    [Required]
    public string Password { get; init; } = "";
}

public sealed record AuthResponse(string Token, DateTime ExpiresAt, Guid UserId, string Email, string DisplayName, bool IsGuest);

public sealed record UserDto(Guid Id, string Email, string DisplayName, bool IsGuest);

// ---------- Boards ----------

public sealed record CardDto(
    Guid Id,
    Guid ColumnId,
    string Title,
    string Description,
    string? Color,
    int Position,
    DateTime UpdatedAt);

public sealed record ColumnDto(Guid Id, string Title, int Position, IReadOnlyList<CardDto> Cards);

public sealed record MemberDto(Guid UserId, string DisplayName, BoardRole Role, bool IsGuest);

public sealed record BoardDto(
    Guid Id,
    string Title,
    string ShareToken,
    BoardRole MyRole,
    IReadOnlyList<ColumnDto> Columns,
    IReadOnlyList<MemberDto> Members,
    DateTime UpdatedAt);

public sealed record BoardSummaryDto(
    Guid Id,
    string Title,
    BoardRole MyRole,
    int MemberCount,
    int CardCount,
    DateTime UpdatedAt);

public sealed record JoinResultDto(Guid BoardId, bool AlreadyMember);

public sealed record ShareTokenDto(string ShareToken);

public sealed record CreateBoardRequest
{
    [Required, MaxLength(120)]
    public string Title { get; init; } = "";

    /// <summary>Start with To do / In progress / Done.</summary>
    public bool WithDefaultColumns { get; init; } = true;
}

public sealed record RenameRequest
{
    [Required, MaxLength(120)]
    public string Title { get; init; } = "";
}

public sealed record MoveColumnRequest
{
    [Range(0, int.MaxValue)]
    public int ToIndex { get; init; }
}

public sealed record CreateCardRequest
{
    public Guid ColumnId { get; init; }

    [Required, MaxLength(200)]
    public string Title { get; init; } = "";

    [MaxLength(5000)]
    public string? Description { get; init; }
}

/// <summary>Partial update: only non-null fields change. Color "" clears the label.</summary>
public sealed record UpdateCardRequest
{
    [MaxLength(200)]
    public string? Title { get; init; }

    [MaxLength(5000)]
    public string? Description { get; init; }

    [MaxLength(20)]
    public string? Color { get; init; }
}

public sealed record MoveCardRequest
{
    public Guid ToColumnId { get; init; }

    [Range(0, int.MaxValue)]
    public int ToIndex { get; init; }
}
