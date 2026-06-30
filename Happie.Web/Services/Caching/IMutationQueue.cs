namespace Happie.Web.Services.Caching;

/// <summary>Abstracts IndexedDB mutation queue operations for offline mutation queueing.</summary>
public interface IMutationQueue
{
    /// <summary>Initializes the IndexedDB database and checks availability.</summary>
    Task InitializeAsync();

    /// <summary>Enqueues a mutation for later replay. No-op if IndexedDB is unavailable.</summary>
    Task EnqueueAsync(string householdId, QueuedMutation mutation);

    /// <summary>Dequeues the oldest mutation for the household (FIFO). Returns null if empty or unavailable.</summary>
    Task<QueuedMutation?> DequeueAsync(string householdId);

    /// <summary>Returns all queued mutations for the household without removing them.</summary>
    Task<IReadOnlyList<QueuedMutation>> PeekAllAsync(string householdId);
}
