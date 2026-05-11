using Happie.Api.Infrastructure;
using Happie.Api.Repositories.Entities;
using Happie.Api.Repositories.Mappers;
using Happie.Shared.Domain;

namespace Happie.Api.Repositories;

/// <summary>Repository for comments backed by Azure Table Storage.</summary>
public class CommentRepository : BaseRepository<CommentEntity>, ICommentRepository
{
    private const string TableName = "Comments";

    private readonly ICommentMapper _mapper;

    /// <summary>Initializes a new instance of <see cref="CommentRepository"/>.</summary>
    public CommentRepository(ITableStorageClient client, ICommentMapper mapper) : base(client, TableName)
    {
        _mapper = mapper;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Comment>> GetByDateAsync(Guid householdId, DateOnly date, CancellationToken ct = default)
    {
        var entities = await QueryByRowKeyPrefixAsync(householdId.ToString(), $"{date:yyyy-MM-dd}#", ct);
        return entities.Select(e => _mapper.ToModel(householdId, e)).ToList();
    }

    /// <inheritdoc/>
    public async Task<Comment?> GetAsync(Guid householdId, DateOnly date, Guid housemateId, CancellationToken ct = default)
    {
        var entity = await GetAsync(householdId.ToString(), $"{date:yyyy-MM-dd}#{housemateId}", ct);
        return entity is null ? null : _mapper.ToModel(householdId, entity);
    }

    /// <inheritdoc/>
    public Task UpsertAsync(Comment comment, CancellationToken ct = default)
        => UpsertAsync(_mapper.ToEntity(comment), ct);

    /// <inheritdoc/>
    public Task DeleteAsync(Guid householdId, DateOnly date, Guid housemateId, CancellationToken ct = default)
        => DeleteAsync(householdId.ToString(), $"{date:yyyy-MM-dd}#{housemateId}", ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Comment>> GetAllByHouseholdAsync(Guid householdId, CancellationToken ct = default)
    {
        var entities = await QueryByPartitionAsync(householdId.ToString(), ct);
        return entities.Select(e => _mapper.ToModel(householdId, e)).ToList();
    }
}
