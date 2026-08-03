using Happie.Api.Domain;

namespace Happie.Api.Infrastructure.Repositories;

/// <summary>Repository for cooking instructions.</summary>
public interface ICookingInstructionRepository
{
    /// <summary>Gets all cooking instructions for a saved dish.</summary>
    Task<IReadOnlyList<CookingInstruction>> GetAllAsync(Guid householdId, Guid savedDishId, CancellationToken cancellationToken = default);

    /// <summary>Upserts multiple cooking instructions.</summary>
    Task BatchUpsertAsync(IReadOnlyList<CookingInstruction> instructions, CancellationToken cancellationToken = default);

    /// <summary>Deletes multiple cooking instructions by their keys.</summary>
    Task BatchDeleteAsync(Guid householdId, IReadOnlyList<(Guid SavedDishId, Guid InstructionId)> keys, CancellationToken cancellationToken = default);
}
