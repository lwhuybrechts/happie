using Happie.Api.Domain;
using Happie.Api.Infrastructure.Entities;
using Happie.Api.Infrastructure.Mappers;

namespace Happie.Api.Infrastructure.Repositories;

/// <summary>Repository for cooking instructions backed by Azure Table Storage.</summary>
public class CookingInstructionRepository : BaseRepository<CookingInstructionEntity>, ICookingInstructionRepository
{
    private const string TableName = "CookingInstructions";
    private readonly ICookingInstructionMapper _mapper;

    /// <summary>Initializes a new instance of <see cref="CookingInstructionRepository"/>.</summary>
    public CookingInstructionRepository(ITableStorageClient client, ICookingInstructionMapper mapper) : base(client, TableName)
    {
        _mapper = mapper;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CookingInstruction>> GetAllAsync(Guid householdId, Guid savedDishId, CancellationToken cancellationToken = default)
    {
        var entities = await QueryByRowKeyPrefixAsync(householdId.ToString(), $"{savedDishId}_", cancellationToken);
        return entities.Select(x => _mapper.ToModel(householdId, x)).ToList();
    }

    /// <inheritdoc/>
    public async Task BatchUpsertAsync(IReadOnlyList<CookingInstruction> instructions, CancellationToken cancellationToken = default)
    {
        foreach (var instruction in instructions)
            await UpsertAsync(_mapper.ToEntity(instruction), cancellationToken);
    }

    /// <inheritdoc/>
    public async Task BatchDeleteAsync(Guid householdId, IReadOnlyList<(Guid SavedDishId, Guid InstructionId)> keys, CancellationToken cancellationToken = default)
    {
        foreach (var key in keys)
            await DeleteAsync(householdId.ToString(), $"{key.SavedDishId}_{key.InstructionId}", cancellationToken);
    }
}
