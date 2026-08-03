namespace Happie.Api.Results;

/// <summary>Outcome of an update ingredients operation.</summary>
public enum UpdateIngredientsOutcome
{
    /// <summary>The ingredients were updated successfully.</summary>
    Success,

    /// <summary>The saved dish was not found or has been deleted.</summary>
    NotFound,

    /// <summary>The request contained invalid data.</summary>
    ValidationError,
}
