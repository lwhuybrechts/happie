namespace Happie.Api.Results;

/// <summary>Outcome of an update recipe summary operation.</summary>
public enum UpdateSummaryOutcome
{
    /// <summary>The recipe summary was updated successfully.</summary>
    Success,

    /// <summary>The saved dish was not found or has been deleted.</summary>
    NotFound,

    /// <summary>The request contained invalid data.</summary>
    ValidationError,
}
