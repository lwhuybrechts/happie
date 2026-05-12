namespace Happie.Api.Results;

/// <summary>Outcome of an update housemate operation.</summary>
public enum UpdateHousemateOutcome
{
    /// <summary>The housemate was updated successfully.</summary>
    Success,

    /// <summary>The housemate was not found or has been deleted.</summary>
    NotFound,

    /// <summary>The request contained invalid data.</summary>
    ValidationError,

    /// <summary>The requested color is already in use by another active housemate.</summary>
    ColorConflict,
}
