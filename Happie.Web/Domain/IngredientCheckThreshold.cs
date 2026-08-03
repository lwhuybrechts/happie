namespace Happie.Web.Domain;

/// <summary>Determines the "Check all" / "Uncheck all" label based on ingredient checked states.</summary>
public static class IngredientCheckThreshold
{
    /// <summary>
    /// Returns true when more than 50% of ingredients are checked, indicating the label should be "Uncheck all".
    /// Returns false when 50% or fewer are checked, indicating the label should be "Check all".
    /// </summary>
    public static bool IsAboveHalf(int totalCount, int checkedCount)
    {
        if (totalCount <= 0)
            return false;

        return checkedCount > totalCount / 2.0;
    }
}
