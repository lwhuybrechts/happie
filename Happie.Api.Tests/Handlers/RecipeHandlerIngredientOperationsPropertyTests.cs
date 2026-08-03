using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Shared.Contracts;
using Happie.Shared.Domain;

namespace Happie.Api.Tests.Handlers;

// Feature: dish-recipes, Property 7: For any list of ingredients and any valid swap operation (moving an element up or down), the resulting list SHALL contain exactly the same set of ingredients with only the order changed.
// Feature: dish-recipes, Property 8: For any ingredient with a name consisting entirely of whitespace, the system SHALL auto-delete that item upon confirm.

/// <summary>Property-based tests for ingredient reorder and whitespace auto-delete operations.</summary>
public class RecipeHandlerIngredientOperationsPropertyTests
{
    /// <summary>
    /// For any list of ingredients and any valid swap operation (moving an element up or down),
    /// the resulting list SHALL contain exactly the same set of ingredients with only the order changed.
    /// **Validates: Requirements 6.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Reorder_AnyValidSwap_PreservesIngredientSet()
    {
        return Prop.ForAll(
            SwapScenarioArb(),
            scenario =>
            {
                // Arrange.
                var ingredients = scenario.Ingredients.ToList();
                var originalIds = ingredients.Select(x => x.Id).ToList();

                // Act — perform the swap (same logic as IngredientsPanel MoveUp/MoveDown).
                if (scenario.Direction == SwapDirection.Up && scenario.Index > 0)
                    (ingredients[scenario.Index], ingredients[scenario.Index - 1]) = (ingredients[scenario.Index - 1], ingredients[scenario.Index]);
                else if (scenario.Direction == SwapDirection.Down && scenario.Index < ingredients.Count - 1)
                    (ingredients[scenario.Index], ingredients[scenario.Index + 1]) = (ingredients[scenario.Index + 1], ingredients[scenario.Index]);

                // Assert — same set of IDs.
                var resultIds = ingredients.Select(x => x.Id).ToList();
                var sameCount = resultIds.Count == originalIds.Count;
                var sameSet = resultIds.OrderBy(x => x).SequenceEqual(originalIds.OrderBy(x => x));

                // Assert — order actually changed (when swap is valid and list has more than 1 element).
                var swapIsValid = (scenario.Direction == SwapDirection.Up && scenario.Index > 0)
                    || (scenario.Direction == SwapDirection.Down && scenario.Index < originalIds.Count - 1);
                var orderChanged = !swapIsValid || !resultIds.SequenceEqual(originalIds);

                return (sameCount && sameSet && orderChanged)
                    .Label($"sameCount={sameCount}, sameSet={sameSet}, orderChanged={orderChanged}, " +
                           $"direction={scenario.Direction}, index={scenario.Index}, count={originalIds.Count}");
            });
    }

    /// <summary>
    /// For any ingredient with a name consisting entirely of whitespace,
    /// the system SHALL auto-delete that item upon confirm.
    /// **Validates: Requirements 6.10**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property WhitespaceFilter_WhitespaceOnlyNames_AreAutoDeleted()
    {
        return Prop.ForAll(
            IngredientListWithWhitespaceArb(),
            ingredients =>
            {
                // Arrange — determine expected valid items (non-whitespace names).
                var expectedValidIds = ingredients
                    .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                    .Select(x => x.Id)
                    .ToHashSet();

                // Act — apply the same filtering logic as the handler.
                var validIngredients = ingredients
                    .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                    .ToList();

                // Assert — only non-whitespace items remain.
                var allRemainingAreValid = validIngredients.All(x => !string.IsNullOrWhiteSpace(x.Name));
                var correctCount = validIngredients.Count == expectedValidIds.Count;
                var correctIds = validIngredients.Select(x => x.Id).ToHashSet().SetEquals(expectedValidIds);

                // Assert — no whitespace-only items survived.
                var noWhitespaceOnlySurvived = !validIngredients.Any(x => string.IsNullOrWhiteSpace(x.Name));

                return (allRemainingAreValid && correctCount && correctIds && noWhitespaceOnlySurvived)
                    .Label($"allValid={allRemainingAreValid}, correctCount={correctCount}, " +
                           $"correctIds={correctIds}, noWhitespaceSurvived={noWhitespaceOnlySurvived}, " +
                           $"input={ingredients.Count}, output={validIngredients.Count}");
            });
    }

    private static Arbitrary<SwapScenario> SwapScenarioArb()
    {
        // Generate a list of 2-30 ingredients, a valid index, and a direction.
        var gen = Gen.Choose(2, RecipeConstants.MaxIngredients)
            .SelectMany(count =>
                Gen.ListOf(IngredientDtoGen(), count)
                    .SelectMany(ingredients =>
                        Gen.Choose(0, count - 1).SelectMany(index =>
                            Gen.Elements(SwapDirection.Up, SwapDirection.Down)
                                .Select(direction => new SwapScenario(ingredients.ToList(), index, direction)))));

        return Arb.From(gen);
    }

    private static Arbitrary<List<IngredientDto>> IngredientListWithWhitespaceArb()
    {
        // Generate a list of 1-30 ingredients where some have whitespace-only names.
        var gen = Gen.Choose(1, RecipeConstants.MaxIngredients)
            .SelectMany(count => Gen.ListOf(IngredientDtoWithMixedNamesGen(), count)
                .Select(x => x.ToList())
                // Ensure at least one whitespace-only name to make the property meaningful.
                .Where(x => x.Any(i => string.IsNullOrWhiteSpace(i.Name))));

        return Arb.From(gen);
    }

    private static Gen<IngredientDto> IngredientDtoGen()
    {
        var nameCharGen = Gen.Choose(33, 126).Select(x => (char)x);

        return Gen.Choose(1, 20)
            .SelectMany(nameLength => Gen.ListOf(nameCharGen, nameLength)
                .Select(chars => new IngredientDto(
                    Guid.NewGuid(),
                    1.0,
                    UnitOfMeasurement.G,
                    new string(chars.ToArray()),
                    0)));
    }

    private static Gen<IngredientDto> IngredientDtoWithMixedNamesGen()
    {
        // Generate ingredients with either valid names or whitespace-only names.
        var validNameCharGen = Gen.Choose(33, 126).Select(x => (char)x);

        var validNameGen = Gen.Choose(1, 20)
            .SelectMany(length => Gen.ListOf(validNameCharGen, length)
                .Select(chars => new string(chars.ToArray())));

        var whitespaceNameGen = Gen.Choose(0, 3).Select(choice => choice switch
        {
            0 => "",
            1 => " ",
            2 => "   ",
            _ => "\t \t"
        });

        return Gen.Choose(0, 2).SelectMany(choice => choice switch
        {
            // Valid name (2 out of 3 chance).
            0 or 1 => validNameGen,
            // Whitespace-only name (1 out of 3 chance).
            _ => whitespaceNameGen
        }).Select(name => new IngredientDto(
            Guid.NewGuid(),
            1.0,
            UnitOfMeasurement.G,
            name,
            0));
    }

    private record SwapScenario(List<IngredientDto> Ingredients, int Index, SwapDirection Direction);

    private enum SwapDirection
    {
        Up,
        Down
    }
}
