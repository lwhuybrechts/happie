using Happie.Api.Domain;
using Happie.Api.Infrastructure.Entities;
using Happie.Api.Infrastructure.Mappers;

namespace Happie.Api.Infrastructure.Repositories;

/// <summary>Repository for recipe summaries backed by Azure Table Storage.</summary>
public class RecipeSummaryRepository : BaseRepository<RecipeSummaryEntity>, IRecipeSummaryRepository
{
    private const string TableName = "RecipeSummaries";
    private readonly IRecipeSummaryMapper _mapper;

    /// <summary>Initializes a new instance of <see cref="RecipeSummaryRepository"/>.</summary>
    public RecipeSummaryRepository(ITableStorageClient client, IRecipeSummaryMapper mapper) : base(client, TableName)
    {
        _mapper = mapper;
    }

    /// <inheritdoc/>
    public async Task<RecipeSummary?> GetAsync(Guid householdId, Guid savedDishId, CancellationToken cancellationToken = default)
    {
        var entity = await GetAsync(householdId.ToString(), savedDishId.ToString(), cancellationToken);
        return entity is null ? null : _mapper.ToModel(householdId, entity);
    }

    /// <inheritdoc/>
    public Task UpsertAsync(RecipeSummary summary, CancellationToken cancellationToken = default)
        => UpsertAsync(_mapper.ToEntity(summary), cancellationToken);

    /// <inheritdoc/>
    public Task DeleteAsync(Guid householdId, Guid savedDishId, CancellationToken cancellationToken = default)
        => DeleteAsync(householdId.ToString(), savedDishId.ToString(), cancellationToken);
}
