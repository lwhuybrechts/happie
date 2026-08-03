using Happie.Api.Domain;
using Happie.Api.Infrastructure.Entities;

namespace Happie.Api.Infrastructure.Mappers;

/// <summary>Maps between <see cref="RecipeSummaryEntity"/> and <see cref="RecipeSummary"/>.</summary>
public class RecipeSummaryMapper : IRecipeSummaryMapper
{
    /// <inheritdoc/>
    public RecipeSummary ToModel(Guid householdId, RecipeSummaryEntity entity) =>
        new(householdId, Guid.Parse(entity.RowKey), entity.Summary, entity.CookingDurationMinutes, entity.Servings);

    /// <inheritdoc/>
    public RecipeSummaryEntity ToEntity(RecipeSummary summary)
    {
        var entity = new RecipeSummaryEntity(summary.HouseholdId, summary.SavedDishId);
        entity.Summary = summary.Summary;
        entity.CookingDurationMinutes = summary.CookingDurationMinutes;
        entity.Servings = summary.Servings;
        return entity;
    }
}
