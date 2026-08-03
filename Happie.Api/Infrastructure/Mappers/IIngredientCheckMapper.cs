using Happie.Api.Domain;
using Happie.Api.Infrastructure.Entities;

namespace Happie.Api.Infrastructure.Mappers;

/// <summary>Maps between <see cref="IngredientCheckEntity"/> and <see cref="IngredientCheck"/>.</summary>
public interface IIngredientCheckMapper
{
    /// <summary>Maps an <see cref="IngredientCheckEntity"/> to an <see cref="IngredientCheck"/> domain record.</summary>
    IngredientCheck ToModel(Guid householdId, IngredientCheckEntity entity);

    /// <summary>Maps an <see cref="IngredientCheck"/> domain record to an <see cref="IngredientCheckEntity"/>.</summary>
    IngredientCheckEntity ToEntity(IngredientCheck check);
}
