namespace Happie.Api.Results;

/// <summary>Outcome of an update saved dish operation.</summary>
public enum SavedDishUpdateOutcome
{
    /// <summary>The saved dish was updated successfully.</summary>
    Updated,

    /// <summary>Another dish with the same description already exists.</summary>
    AlreadyExists,

    /// <summary>The saved dish was not found or has been deleted.</summary>
    NotFound,

    /// <summary>The request contained invalid data.</summary>
    ValidationError,
}
