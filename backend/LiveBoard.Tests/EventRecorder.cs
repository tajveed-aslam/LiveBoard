using System.Collections.Concurrent;
using System.Text.Json;
using LiveBoard.Api.Models;
using Microsoft.AspNetCore.SignalR.Client;

namespace LiveBoard.Tests;

/// <summary>Records every board event a hub connection receives, and waits for specific ones.</summary>
public sealed class EventRecorder
{
    private readonly ConcurrentQueue<(string Name, JsonElement Payload)> _events = new();
    private readonly SemaphoreSlim _signal = new(0);

    public EventRecorder(HubConnection connection)
    {
        string[] names =
        [
            BoardEvents.BoardUpdated, BoardEvents.BoardDeleted, BoardEvents.ColumnCreated, BoardEvents.ColumnUpdated,
            BoardEvents.ColumnDeleted, BoardEvents.ColumnsReordered, BoardEvents.CardCreated, BoardEvents.CardUpdated,
            BoardEvents.CardDeleted, BoardEvents.CardMoved, BoardEvents.MemberJoined, BoardEvents.PresenceChanged,
            BoardEvents.EditingChanged,
        ];
        foreach (var name in names)
        {
            connection.On<JsonElement>(name, payload =>
            {
                _events.Enqueue((name, payload));
                _signal.Release();
            });
        }
    }

    public IReadOnlyList<string> Names => _events.Select(e => e.Name).ToList();

    /// <summary>Waits until an event with this name (and matching payload) has arrived; returns it typed.</summary>
    public async Task<T> WaitForAsync<T>(string name, Func<T, bool>? match = null, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (true)
        {
            foreach (var (n, payload) in _events)
            {
                if (n != name)
                    continue;
                var typed = payload.Deserialize<T>(TestApp.Json)!;
                if (match is null || match(typed))
                    return typed;
            }

            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero || !await _signal.WaitAsync(remaining))
                throw new TimeoutException($"No matching '{name}' event within {timeoutMs} ms. Received: {string.Join(", ", Names)}");
        }
    }
}
