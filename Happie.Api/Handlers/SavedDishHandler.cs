using Happie.Api.Domain;
using Happie.Api.Infrastructure.Repositories;
using Happie.Api.Results;
using Microsoft.Extensions.Logging;

namespace Happie.Api.Handlers;

/// <summary>Handles saved dish management operations.</summary>
public class SavedDishHandler : ISavedDishHandler
{
    private readonly ISavedDishRepository _savedDishRepository;
    private readonly IDishRepository _dishRepository;
    private readonly ILogger<SavedDishHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="SavedDishHandler"/>.</summary>
    public SavedDishHandler(
        ISavedDishRepository savedDishRepository,
        IDishRepository dishRepository,
        ILogger<SavedDishHandler> logger)
    {
        _savedDishRepository = savedDishRepository;
        _dishRepository = dishRepository;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SavedDish>> GetAllActiveAsync(Guid householdId, CancellationToken cancellationToken = default)
    {
        var allDishes = await _savedDishRepository.GetAllAsync(householdId, cancellationToken);

        return allDishes
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.Description, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<SavedDishCreateResult> CreateAsync(Guid householdId, string description, CancellationToken cancellationToken = default)
    {
        var trimmed = description.Trim();

        if (trimmed.Length == 0 || trimmed.Length > 100)
            return new SavedDishCreateResult(SavedDishCreateOutcome.ValidationError);

        var allDishes = await _savedDishRepository.GetAllAsync(householdId, cancellationToken);

        // Check for an existing match (case-insensitive).
        var existingMatch = allDishes.FirstOrDefault(x =>
            string.Equals(x.Description, trimmed, StringComparison.OrdinalIgnoreCase));

        if (existingMatch is not null)
        {
            if (!existingMatch.IsDeleted)
                return new SavedDishCreateResult(SavedDishCreateOutcome.AlreadyExists);

            // Reactivate soft-deleted match.
            var reactivated = existingMatch with { IsDeleted = false, Description = trimmed };
            await _savedDishRepository.UpsertAsync(reactivated, cancellationToken);
            await ConvertMatchingDishRecordsAsync(householdId, reactivated, cancellationToken);
            return new SavedDishCreateResult(SavedDishCreateOutcome.Reactivated, reactivated);
        }

        // Create new saved dish.
        var newDish = new SavedDish(Guid.NewGuid(), householdId, trimmed, false);
        await _savedDishRepository.UpsertAsync(newDish, cancellationToken);
        await ConvertMatchingDishRecordsAsync(householdId, newDish, cancellationToken);
        return new SavedDishCreateResult(SavedDishCreateOutcome.Created, newDish);
    }

    /// <inheritdoc/>
    public async Task<SavedDishUpdateResult> UpdateAsync(Guid householdId, Guid savedDishId, string description, CancellationToken cancellationToken = default)
    {
        var trimmed = description.Trim();

        if (trimmed.Length == 0 || trimmed.Length > 100)
            return new SavedDishUpdateResult(SavedDishUpdateOutcome.ValidationError);

        var target = await _savedDishRepository.GetAsync(householdId, savedDishId, cancellationToken);

        if (target is null || target.IsDeleted)
            return new SavedDishUpdateResult(SavedDishUpdateOutcome.NotFound);

        // Check uniqueness excluding self (case-insensitive).
        var allDishes = await _savedDishRepository.GetAllAsync(householdId, cancellationToken);
        var conflict = allDishes.FirstOrDefault(x =>
            x.Id != savedDishId &&
            string.Equals(x.Description, trimmed, StringComparison.OrdinalIgnoreCase));

        if (conflict is not null)
            return new SavedDishUpdateResult(SavedDishUpdateOutcome.AlreadyExists);

        var updated = target with { Description = trimmed };
        await _savedDishRepository.UpsertAsync(updated, cancellationToken);
        return new SavedDishUpdateResult(SavedDishUpdateOutcome.Updated, updated);
    }

    /// <inheritdoc/>
    public async Task<SavedDishDeleteResult> DeleteAsync(Guid householdId, Guid savedDishId, CancellationToken cancellationToken = default)
    {
        var target = await _savedDishRepository.GetAsync(householdId, savedDishId, cancellationToken);

        if (target is null || target.IsDeleted)
            return SavedDishDeleteResult.NotFound;

        var softDeleted = target with { IsDeleted = true };
        await _savedDishRepository.UpsertAsync(softDeleted, cancellationToken);
        return SavedDishDeleteResult.Deleted;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetSuggestionsAsync(Guid householdId, CancellationToken cancellationToken = default)
    {
        var dishRecords = await _dishRepository.GetAllByPartitionAsync(householdId, cancellationToken);
        var allSavedDishes = await _savedDishRepository.GetAllAsync(householdId, cancellationToken);

        // Build a set of all saved dish descriptions (active + soft-deleted) for exclusion.
        var savedDescriptions = allSavedDishes
            .Select(x => x.Description.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Filter DishRecords: non-empty description, not matching any saved dish.
        var candidates = dishRecords
            .Where(x => !string.IsNullOrWhiteSpace(x.Description) &&
                        !savedDescriptions.Contains(x.Description.Trim()))
            .OrderByDescending(x => x.Date)
            .ToList();

        // Take distinct descriptions (case-insensitive), limit to 5.
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var suggestions = new List<string>();

        foreach (var record in candidates)
        {
            var trimmed = record.Description.Trim();
            if (seen.Add(trimmed))
            {
                suggestions.Add(trimmed);
                if (suggestions.Count >= 5)
                    break;
            }
        }

        return suggestions;
    }

    /// <summary>Converts matching DishRecords to reference the saved dish (retroactive conversion).</summary>
    private async Task ConvertMatchingDishRecordsAsync(Guid householdId, SavedDish savedDish, CancellationToken cancellationToken)
    {
        var dishRecords = await _dishRepository.GetAllByPartitionAsync(householdId, cancellationToken);

        var matchingRecords = dishRecords
            .Where(x => !string.IsNullOrWhiteSpace(x.Description) &&
                        string.Equals(x.Description.Trim(), savedDish.Description, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var record in matchingRecords)
        {
            try
            {
                var converted = record with { Description = string.Empty };
                await _dishRepository.UpsertAsync(converted, cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Failed to convert DishRecord {Date} to reference SavedDish {SavedDishId} in household {HouseholdId}.",
                    record.Date,
                    savedDish.Id,
                    householdId);
            }
        }
    }
}
