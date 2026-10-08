using System.Security.Cryptography;

namespace LiveBoard.Api.Services;

public static class DisplayNames
{
    private static readonly string[] Adjectives =
        ["Swift", "Bright", "Calm", "Clever", "Bold", "Quiet", "Lucky", "Brave", "Sunny", "Keen", "Witty", "Nimble"];

    private static readonly string[] Animals =
        ["Otter", "Falcon", "Panda", "Lynx", "Heron", "Badger", "Koala", "Fox", "Owl", "Dolphin", "Tiger", "Wren"];

    /// <summary>A friendly random name for guests, e.g. "Swift Otter", so collaborators can tell them apart.</summary>
    public static string ForGuest() =>
        $"{Adjectives[RandomNumberGenerator.GetInt32(Adjectives.Length)]} {Animals[RandomNumberGenerator.GetInt32(Animals.Length)]}";

    /// <summary>The requested name, or the email's local part ("jane.doe" → "Jane Doe").</summary>
    public static string ForUser(string? requested, string email)
    {
        var name = requested?.Trim();
        if (!string.IsNullOrEmpty(name))
            return name.Length <= 40 ? name : name[..40];

        var local = email.Split('@')[0];
        var words = local.Split(['.', '_', '-', '+'], StringSplitOptions.RemoveEmptyEntries)
            .Select(w => char.ToUpperInvariant(w[0]) + w[1..]);
        var derived = string.Join(' ', words);
        if (derived.Length == 0)
            derived = "User";
        return derived.Length <= 40 ? derived : derived[..40];
    }
}
