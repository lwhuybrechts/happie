using Happie.Api.Domain;

namespace Happie.Api.Results;

/// <summary>Result returned by a create saved dish operation.</summary>
public record SavedDishCreateResult(
    SavedDishCreateOutcome Outcome,
    SavedDish? SavedDish = null
);
