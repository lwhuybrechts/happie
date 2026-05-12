using Happie.Shared.Domain;

namespace Happie.Api.Infrastructure.Repositories;

/// <summary>Repository for comments.</summary>
public interface ICommentRepository
{
    /// <summary>Gets all comments for a household on a specific date.</summary>
    Task<IReadOnlyList<Comment>> GetByDateAsync(Guid householdId, DateOnly date, CancellationToken ct = default);

    /// <summary>Gets a single comment for a housemate on a specific date, or null if not found.</summary>
    Task<Comment?> GetAsync(Guid householdId, DateOnly date, Guid housemateId, CancellationToken ct = default);

    /// <summary>Upserts a comment.</summary>
    Task UpsertAsync(Comment comment, CancellationToken ct = default);

    /// <summary>Deletes a comment.</summary>
    Task DeleteAsync(Guid householdId, DateOnly date, Guid housemateId, CancellationToken ct = default);

    /// <summary>Gets all comments for a household across all dates.</summary>
    Task<IReadOnlyList<Comment>> GetAllByHouseholdAsync(Guid householdId, CancellationToken ct = default);
}
