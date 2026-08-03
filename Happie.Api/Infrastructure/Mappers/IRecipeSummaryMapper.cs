using Happie.Api.Domain;
using Happie.Api.Infrastructure.Entities;

namespace Happie.Api.Infrastructure.Mappers;

/// <summary>Maps between <see cref="RecipeSummaryEntity"/> and <see cref="RecipeSummary"/>.</summary>
public interface IRecipeSummaryMapper
{
    /// <summary>Maps a <see cref="RecipeSummaryEntity"/> to a <see cref="RecipeSummary"/> domain record.</summary>
    RecipeSummary ToModel(Guid householdId, RecipeSummaryEntity entity);

    /// <summary>Maps a <see cref="RecipeSummary"/> domain record to a <see cref="RecipeSummaryEntity"/>.</summary>
    RecipeSummaryEntity ToEntity(RecipeSummary summary);
}
