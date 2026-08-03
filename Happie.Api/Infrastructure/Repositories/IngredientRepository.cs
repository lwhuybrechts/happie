using Happie.Api.Domain;
using Happie.Api.Infrastructure.Entities;
using Happie.Api.Infrastructure.Mappers;

namespace Happie.Api.Infrastructure.Repositories;

/// <summary>Repository for recipe ingredients backed by Azure Table Storage.</summary>
public class IngredientRepository : BaseRepository<IngredientEntity>, IIngredientRepository
{
    private const string TableName = "Ingredients";
    private readonly IIngredientMapper _mapper;

    /// <summary>Initializes a new instance of <see cref="IngredientRepository"/>.</summary>
    public IngredientRepository(ITableStorageClient client, IIngredientMapper mapper) : base(client, TableName)
    {
        _mapper = mapper;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Ingredient>> GetAllAsync(Guid householdId, Guid savedDishId, CancellationToken cancellationToken = default)
    {
        var entities = await QueryByRowKeyPrefixAsync(householdId.ToString(), $"{savedDishId}_", cancellationToken);
        return entities.Select(x => _mapper.ToModel(householdId, x)).ToList();
    }

    /// <inheritdoc/>
    public Task UpsertAsync(Ingredient ingredient, CancellationToken cancellationToken = default)
        => UpsertAsync(_mapper.ToEntity(ingredient), cancellationToken);

    /// <inheritdoc/>
    public Task DeleteAsync(Guid householdId, Guid savedDishId, Guid ingredientId, CancellationToken cancellationToken = default)
        => DeleteAsync(householdId.ToString(), $"{savedDishId}_{ingredientId}", cancellationToken);

    /// <inheritdoc/>
    public async Task BatchUpsertAsync(IReadOnlyList<Ingredient> ingredients, CancellationToken cancellationToken = default)
    {
        foreach (var ingredient in ingredients)
            await UpsertAsync(_mapper.ToEntity(ingredient), cancellationToken);
    }

    /// <inheritdoc/>
    public async Task BatchDeleteAsync(Guid householdId, IReadOnlyList<(Guid SavedDishId, Guid IngredientId)> keys, CancellationToken cancellationToken = default)
    {
        foreach (var key in keys)
            await DeleteAsync(householdId.ToString(), $"{key.SavedDishId}_{key.IngredientId}", cancellationToken);
    }
}
