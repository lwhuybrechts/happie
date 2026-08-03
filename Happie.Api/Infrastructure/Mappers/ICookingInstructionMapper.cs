using Happie.Api.Domain;
using Happie.Api.Infrastructure.Entities;

namespace Happie.Api.Infrastructure.Mappers;

/// <summary>Maps between <see cref="CookingInstructionEntity"/> and <see cref="CookingInstruction"/>.</summary>
public interface ICookingInstructionMapper
{
    /// <summary>Maps a <see cref="CookingInstructionEntity"/> to a <see cref="CookingInstruction"/> domain record.</summary>
    CookingInstruction ToModel(Guid householdId, CookingInstructionEntity entity);

    /// <summary>Maps a <see cref="CookingInstruction"/> domain record to a <see cref="CookingInstructionEntity"/>.</summary>
    CookingInstructionEntity ToEntity(CookingInstruction instruction);
}
