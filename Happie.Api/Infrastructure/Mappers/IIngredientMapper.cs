using Happie.Api.Domain;
using Happie.Api.Infrastructure.Entities;

namespace Happie.Api.Infrastructure.Mappers;

/// <summary>Maps between <see cref="IngredientEntity"/> and <see cref="Ingredient"/>.</summary>
public interface IIngredientMapper
{
    /// <summary>Maps an <see cref="IngredientEntity"/> to an <see cref="Ingredient"/> domain record.</summary>
    Ingredient ToModel(Guid householdId, IngredientEntity entity);

    /// <summary>Maps an <see cref="Ingredient"/> domain record to an <see cref="IngredientEntity"/>.</summary>
    IngredientEntity ToEntity(Ingredient ingredient);
}
