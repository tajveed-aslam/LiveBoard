namespace LiveBoard.Api.Options;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Key { get; set; } = "";
    public string Issuer { get; set; } = "LiveBoard";
    public string Audience { get; set; } = "LiveBoard.Client";
    public int ExpiryMinutes { get; set; } = 480;
}

public sealed class DemoOptions
{
    public const string SectionName = "Demo";

    /// <summary>Allow one-click guest accounts for the public live demo.</summary>
    public bool GuestAccessEnabled { get; set; } = true;
    public int GuestTokenMinutes { get; set; } = 240;
    /// <summary>Generous: trying live sync means opening a second guest in another window.</summary>
    public int GuestSessionsPerHourPerIp { get; set; } = 10;
    /// <summary>REST writes (create/edit/move/delete) per user per hour.</summary>
    public int WritesPerHour { get; set; } = 1500;
}

public sealed class BoardLimits
{
    public const string SectionName = "Limits";

    public int MaxBoardsPerUser { get; set; } = 50;
    public int MaxBoardsPerGuest { get; set; } = 10;
    public int MaxColumnsPerBoard { get; set; } = 20;
    public int MaxCardsPerBoard { get; set; } = 500;
}
