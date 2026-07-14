namespace Happie.Api.Results;

/// <summary>The outcome of a dish upsert operation.</summary>
public enum DishUpsertResult
{
    /// <summary>The dish was saved successfully.</summary>
    Success,

    /// <summary>The dish was deleted (both description and savedDishId are null/empty).</summary>
    Deleted,

    /// <summary>Validation failed (e.g., both savedDishId and description provided).</summary>
    ValidationError,

    /// <summary>The referenced saved dish was not found.</summary>
    SavedDishNotFound
}
