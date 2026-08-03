using Happie.Api.Domain;

namespace Happie.Api.Infrastructure.Repositories;

/// <summary>Repository for recipe summaries.</summary>
public interface IRecipeSummaryRepository
{
    /// <summary>Gets the recipe summary for a saved dish, or null if not found.</summary>
    Task<RecipeSummary?> GetAsync(Guid householdId, Guid savedDishId, CancellationToken cancellationToken = default);

    /// <summary>Upserts a recipe summary.</summary>
    Task UpsertAsync(RecipeSummary summary, CancellationToken cancellationToken = default);

    /// <summary>Deletes the recipe summary for a saved dish.</summary>
    Task DeleteAsync(Guid householdId, Guid savedDishId, CancellationToken cancellationToken = default);
}
