using Npgsql;

namespace LiveBoard.Api.Infrastructure;

public static class ConnectionStrings
{
    /// <summary>
    /// Hosted Postgres providers (Neon, Render, ...) hand out postgres:// URLs, which Npgsql doesn't accept.
    /// Converts those to Npgsql key/value form; anything else is returned unchanged.
    /// </summary>
    public static string? NormalizePostgres(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !(value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
              value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)))
            return value;

        var uri = new Uri(value);
        var userInfo = uri.UserInfo.Split(':', 2);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort || uri.Port <= 0 ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
        };

        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var sslMode = query["sslmode"];
        builder.SslMode = sslMode?.ToLowerInvariant() switch
        {
            "disable" => SslMode.Disable,
            "allow" => SslMode.Allow,
            "prefer" => SslMode.Prefer,
            "verify-ca" => SslMode.VerifyCA,
            "verify-full" => SslMode.VerifyFull,
            // Hosted databases require TLS; default to it when a URL is used.
            _ => SslMode.Require,
        };

        builder.ChannelBinding = query["channel_binding"]?.ToLowerInvariant() switch
        {
            "require" => ChannelBinding.Require,
            "disable" => ChannelBinding.Disable,
            _ => ChannelBinding.Prefer,
        };

        return builder.ConnectionString;
    }
}
