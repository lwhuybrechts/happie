using Happie.Api.Domain;
using Happie.Api.Infrastructure.Entities;

namespace Happie.Api.Infrastructure.Mappers;

/// <summary>Maps between <see cref="CookingInstructionEntity"/> and <see cref="CookingInstruction"/>.</summary>
public class CookingInstructionMapper : ICookingInstructionMapper
{
    /// <inheritdoc/>
    public CookingInstruction ToModel(Guid householdId, CookingInstructionEntity entity)
    {
        var parts = entity.RowKey.Split('_');
        var savedDishId = Guid.Parse(parts[0]);
        var instructionId = Guid.Parse(parts[1]);
        return new CookingInstruction(instructionId, householdId, savedDishId, entity.Text, entity.SortOrder);
    }

    /// <inheritdoc/>
    public CookingInstructionEntity ToEntity(CookingInstruction instruction)
    {
        var entity = new CookingInstructionEntity(instruction.HouseholdId, instruction.SavedDishId, instruction.Id);
        entity.Text = instruction.Text;
        entity.SortOrder = instruction.SortOrder;
        return entity;
    }
}
