using Happie.Api.Domain;
using Happie.Api.Infrastructure.Entities;
using Happie.Api.Infrastructure.Mappers;

namespace Happie.Api.Infrastructure.Repositories;

/// <summary>Repository for ingredient check states backed by Azure Table Storage.</summary>
public class IngredientCheckRepository : BaseRepository<IngredientCheckEntity>, IIngredientCheckRepository
{
    private const string TableName = "IngredientChecks";
    private readonly IIngredientCheckMapper _mapper;

    /// <summary>Initializes a new instance of <see cref="IngredientCheckRepository"/>.</summary>
    public IngredientCheckRepository(ITableStorageClient client, IIngredientCheckMapper mapper) : base(client, TableName)
    {
        _mapper = mapper;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IngredientCheck>> GetAllAsync(Guid householdId, Guid savedDishId, CancellationToken cancellationToken = default)
    {
        var entities = await QueryByRowKeyPrefixAsync(householdId.ToString(), $"{savedDishId}_", cancellationToken);
        return entities.Select(x => _mapper.ToModel(householdId, x)).ToList();
    }

    /// <inheritdoc/>
    public Task UpsertAsync(IngredientCheck check, CancellationToken cancellationToken = default)
        => UpsertAsync(_mapper.ToEntity(check), cancellationToken);

    /// <inheritdoc/>
    public Task DeleteAsync(Guid householdId, Guid savedDishId, Guid ingredientId, CancellationToken cancellationToken = default)
        => DeleteAsync(householdId.ToString(), $"{savedDishId}_{ingredientId}", cancellationToken);

    /// <inheritdoc/>
    public async Task BatchDeleteAsync(Guid householdId, IReadOnlyList<(Guid SavedDishId, Guid IngredientId)> keys, CancellationToken cancellationToken = default)
    {
        foreach (var key in keys)
            await DeleteAsync(householdId.ToString(), $"{key.SavedDishId}_{key.IngredientId}", cancellationToken);
    }
}
