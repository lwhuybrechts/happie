using Happie.Shared.Contracts;

namespace Happie.Web.Services.Caching;

/// <summary>Replays queued mutations when connectivity is restored, with retry and rollback semantics.</summary>
public interface ISyncService : IDisposable
{
    /// <summary>Subscribes to connectivity changes and prepares for mutation replay.</summary>
    Task InitializeAsync();

    /// <summary>Raised when a mutation rollback updates the cached DayPlan, passing the rolled-back response and its date.</summary>
    event Action<string, DayPlanResponse>? OnDayPlanRolledBack;
}
