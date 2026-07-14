namespace Happie.Api.Results;

/// <summary>Outcome of a delete saved dish operation.</summary>
public enum SavedDishDeleteResult
{
    /// <summary>The saved dish was soft-deleted successfully.</summary>
    Deleted,

    /// <summary>The saved dish was not found or has already been deleted.</summary>
    NotFound,
}
