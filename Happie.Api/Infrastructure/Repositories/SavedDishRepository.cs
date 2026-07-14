using Happie.Api.Domain;
using Happie.Api.Infrastructure.Entities;
using Happie.Api.Infrastructure.Mappers;

namespace Happie.Api.Infrastructure.Repositories;

/// <summary>Repository for saved dishes backed by Azure Table Storage.</summary>
public class SavedDishRepository : BaseRepository<SavedDishEntity>, ISavedDishRepository
{
    private const string TableName = "SavedDishes";

    private readonly ISavedDishMapper _mapper;

    /// <summary>Initializes a new instance of <see cref="SavedDishRepository"/>.</summary>
    public SavedDishRepository(ITableStorageClient client, ISavedDishMapper mapper) : base(client, TableName)
    {
        _mapper = mapper;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SavedDish>> GetAllAsync(Guid householdId, CancellationToken cancellationToken = default)
    {
        var entities = await QueryByPartitionAsync(householdId.ToString(), cancellationToken);
        return entities.Select(x => _mapper.ToModel(householdId, x)).ToList();
    }

    /// <inheritdoc/>
    public async Task<SavedDish?> GetAsync(Guid householdId, Guid savedDishId, CancellationToken cancellationToken = default)
    {
        var entity = await GetAsync(householdId.ToString(), savedDishId.ToString(), cancellationToken);
        return entity is null ? null : _mapper.ToModel(householdId, entity);
    }

    /// <inheritdoc/>
    public Task UpsertAsync(SavedDish savedDish, CancellationToken cancellationToken = default)
        => UpsertAsync(_mapper.ToEntity(savedDish), cancellationToken);
}
