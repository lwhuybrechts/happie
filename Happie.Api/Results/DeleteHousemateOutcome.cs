namespace Happie.Api.Results;

/// <summary>Outcome of a delete housemate operation.</summary>
public enum DeleteHousemateOutcome
{
    /// <summary>The housemate was deleted successfully (hard or soft).</summary>
    Success,

    /// <summary>The housemate was not found or has already been deleted.</summary>
    NotFound,
}
