namespace Happie.Api.Results;

/// <summary>Outcome of a report version operation.</summary>
public enum ReportVersionOutcome
{
    /// <summary>The version was persisted successfully.</summary>
    Success,

    /// <summary>The version was skipped (local development version).</summary>
    Skipped,

    /// <summary>The housemate was not found or has been soft-deleted.</summary>
    NotFound,

    /// <summary>The version string failed validation.</summary>
    ValidationError,
}
