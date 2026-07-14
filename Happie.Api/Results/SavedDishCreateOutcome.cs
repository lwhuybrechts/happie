namespace Happie.Api.Results;

/// <summary>Outcome of a create saved dish operation.</summary>
public enum SavedDishCreateOutcome
{
    /// <summary>A new saved dish was created.</summary>
    Created,

    /// <summary>A soft-deleted dish with matching description was reactivated.</summary>
    Reactivated,

    /// <summary>An active dish with the same description already exists.</summary>
    AlreadyExists,

    /// <summary>The request contained invalid data.</summary>
    ValidationError,
}
