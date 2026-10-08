using System.Collections.Concurrent;

namespace LiveBoard.Api.Services;

/// <summary>
/// Serialises writes per board inside this process, so two people dragging cards at the same moment can't
/// interleave their read-reorder-save steps and corrupt the positions. Different boards never block each other.
/// A single API instance is assumed (true on the hosting plan used); scaling out would move this to a
/// database-level lock (e.g. SELECT ... FOR UPDATE on the board row) plus a SignalR backplane.
/// </summary>
public sealed class BoardLocks
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();

    public async Task<IDisposable> AcquireAsync(Guid boardId, CancellationToken cancellationToken)
    {
        var semaphore = _locks.GetOrAdd(boardId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);
        return new Releaser(semaphore);
    }

    private sealed class Releaser(SemaphoreSlim semaphore) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
                semaphore.Release();
        }
    }
}
