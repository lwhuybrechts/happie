using Happie.Api.Domain;

namespace Happie.Api.Infrastructure.Repositories;

/// <summary>Repository for day plan dish link records.</summary>
public interface IDayPlanDishLinkRepository
{
    /// <summary>Gets all dish links for a specific date in a household.</summary>
    Task<IReadOnlyList<DayPlanDishLink>> GetByDateAsync(Guid householdId, DateOnly date, CancellationToken cancellationToken = default);

    /// <summary>Replaces all dish links for a specific date in a household.</summary>
    Task ReplaceAllAsync(Guid householdId, DateOnly date, IReadOnlyList<DayPlanDishLink> links, CancellationToken cancellationToken = default);

    /// <summary>Deletes all dish links for a specific date in a household.</summary>
    Task DeleteAllAsync(Guid householdId, DateOnly date, CancellationToken cancellationToken = default);

    /// <summary>Gets all dish links across all dates for a household.</summary>
    Task<IReadOnlyList<DayPlanDishLink>> GetAllByHouseholdAsync(Guid householdId, CancellationToken cancellationToken = default);

    /// <summary>Creates a single dish link.</summary>
    Task CreateAsync(DayPlanDishLink link, CancellationToken cancellationToken = default);
}
