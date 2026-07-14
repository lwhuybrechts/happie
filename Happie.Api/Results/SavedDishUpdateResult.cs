using Happie.Api.Domain;

namespace Happie.Api.Results;

/// <summary>Result returned by an update saved dish operation.</summary>
public record SavedDishUpdateResult(
    SavedDishUpdateOutcome Outcome,
    SavedDish? SavedDish = null
);
