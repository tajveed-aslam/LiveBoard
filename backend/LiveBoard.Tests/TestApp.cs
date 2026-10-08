using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LiveBoard.Api.Data;
using LiveBoard.Api.Hubs;
using LiveBoard.Api.Infrastructure;
using LiveBoard.Api.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LiveBoard.Tests;

/// <summary>
/// The real API running in-memory, backed by a throwaway SQLite file (a file rather than :memory: so parallel
/// requests get their own connections, like they would against PostgreSQL).
/// </summary>
public sealed class TestApp : WebApplicationFactory<Program>
{
    public static readonly JsonSerializerOptions Json = JsonDefaults.Configure(new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"liveboard-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:Key", "test-signing-key-that-is-definitely-long-enough-1234567890");
        builder.UseSetting("ConnectionStrings:Default", "Host=unused");
        builder.UseSetting("Database:MigrateOnStartup", "false");
        builder.UseSetting("Demo:GuestSessionsPerHourPerIp", "1000");
        builder.UseSetting("Demo:WritesPerHour", "100000");

        builder.ConfigureTestServices(services =>
        {
            var npgsql = services.Single(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            services.Remove(npgsql);
            services.AddDbContext<AppDbContext>(o => o.UseSqlite($"Data Source={_dbPath};Default Timeout=30"));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
        return host;
    }

    /// <summary>A signed-in guest with an authorised HttpClient.</summary>
    public async Task<TestUser> NewGuestAsync(bool withSampleBoard = false)
    {
        var client = CreateClient();
        var response = await client.PostAsync($"/api/auth/guest?withSampleBoard={withSampleBoard}", null);
        response.EnsureSuccessStatusCode();
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(Json))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return new TestUser(this, client, auth);
    }

    public HubConnection CreateHubConnection(string token) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(Server.BaseAddress, BoardHub.Path.TrimStart('/')), o =>
            {
                // TestServer has no real sockets; long polling runs through its in-memory handler.
                o.HttpMessageHandlerFactory = _ => Server.CreateHandler();
                o.Transports = HttpTransportType.LongPolling;
                o.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .AddJsonProtocol(o => JsonDefaults.Configure(o.PayloadSerializerOptions))
            .Build();

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { File.Delete(_dbPath); } catch (IOException) { /* best effort */ }
    }
}

public sealed record TestUser(TestApp App, HttpClient Http, AuthResponse Auth)
{
    public Guid Id => Auth.UserId;

    public async Task<T> GetAsync<T>(string url)
    {
        var response = await Http.GetAsync(url);
        await EnsureOk(response);
        return (await response.Content.ReadFromJsonAsync<T>(TestApp.Json))!;
    }

    public async Task<T> SendAsync<T>(HttpMethod method, string url, object? body = null)
    {
        var response = await Http.SendAsync(Request(method, url, body));
        await EnsureOk(response);
        return (await response.Content.ReadFromJsonAsync<T>(TestApp.Json))!;
    }

    public async Task<HttpResponseMessage> RawAsync(HttpMethod method, string url, object? body = null) =>
        await Http.SendAsync(Request(method, url, body));

    public Task<BoardDto> CreateBoardAsync(string title = "Test board") =>
        SendAsync<BoardDto>(HttpMethod.Post, "/api/boards", new { title, withDefaultColumns = true });

    public Task<BoardDto> GetBoardAsync(Guid boardId) => GetAsync<BoardDto>($"/api/boards/{boardId}");

    public Task<CardDto> CreateCardAsync(Guid boardId, Guid columnId, string title) =>
        SendAsync<CardDto>(HttpMethod.Post, $"/api/boards/{boardId}/cards", new { columnId, title });

    public Task<CardMovedEvent> MoveCardAsync(Guid boardId, Guid cardId, Guid toColumnId, int toIndex) =>
        SendAsync<CardMovedEvent>(HttpMethod.Post, $"/api/boards/{boardId}/cards/{cardId}/move", new { toColumnId, toIndex });

    public Task<JoinResultDto> JoinAsync(string shareToken) =>
        SendAsync<JoinResultDto>(HttpMethod.Post, $"/api/boards/join/{shareToken}");

    private static HttpRequestMessage Request(HttpMethod method, string url, object? body) =>
        new(method, url) { Content = body is null ? null : JsonContent.Create(body, options: TestApp.Json) };

    private static async Task EnsureOk(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }
}
