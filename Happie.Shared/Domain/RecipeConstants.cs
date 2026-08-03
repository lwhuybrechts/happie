namespace Happie.Shared.Domain;

/// <summary>Shared constants for recipe-related logic used by both client and server.</summary>
public static class RecipeConstants
{
    /// <summary>Units that represent countable items and display as fractions.</summary>
    public static readonly IReadOnlySet<UnitOfMeasurement> CountBasedUnits = new HashSet<UnitOfMeasurement>
    {
        UnitOfMeasurement.Piece, UnitOfMeasurement.Stalk, UnitOfMeasurement.Clove,
        UnitOfMeasurement.Can, UnitOfMeasurement.Slice, UnitOfMeasurement.Bunch,
        UnitOfMeasurement.Handful
    };

    /// <summary>Units that represent weight or volume and display with decimal places.</summary>
    public static readonly IReadOnlySet<UnitOfMeasurement> WeightVolumeUnits = new HashSet<UnitOfMeasurement>
    {
        UnitOfMeasurement.G, UnitOfMeasurement.Kg, UnitOfMeasurement.Ml,
        UnitOfMeasurement.L, UnitOfMeasurement.Tbsp, UnitOfMeasurement.Tsp,
        UnitOfMeasurement.Pinch, UnitOfMeasurement.Cup
    };

    /// <summary>Maximum number of ingredients allowed per saved dish.</summary>
    public const int MaxIngredients = 30;

    /// <summary>Maximum number of cooking instruction steps allowed per saved dish.</summary>
    public const int MaxInstructions = 15;

    /// <summary>Maximum character length for an ingredient name.</summary>
    public const int MaxIngredientNameLength = 100;

    /// <summary>Maximum character length for a cooking instruction paragraph.</summary>
    public const int MaxInstructionTextLength = 500;

    /// <summary>Maximum character length for a recipe summary.</summary>
    public const int MaxSummaryLength = 250;

    /// <summary>Maximum character length for a dish name.</summary>
    public const int MaxDishNameLength = 100;

    /// <summary>Minimum number of servings allowed.</summary>
    public const int MinServings = 1;

    /// <summary>Maximum number of servings allowed.</summary>
    public const int MaxServings = 25;

    /// <summary>Minimum ingredient amount allowed.</summary>
    public const double MinIngredientAmount = 0.01;

    /// <summary>Maximum ingredient amount allowed.</summary>
    public const double MaxIngredientAmount = 9999;
}
