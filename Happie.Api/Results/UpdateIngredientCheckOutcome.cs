namespace Happie.Api.Results;

/// <summary>Outcome of an update ingredient check operation.</summary>
public enum UpdateIngredientCheckOutcome
{
    /// <summary>The ingredient check was updated successfully.</summary>
    Success,

    /// <summary>The saved dish was not found or has been deleted.</summary>
    NotFound,
}
