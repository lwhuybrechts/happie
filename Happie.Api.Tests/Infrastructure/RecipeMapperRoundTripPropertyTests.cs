using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Api.Domain;
using Happie.Api.Infrastructure.Mappers;
using Happie.Shared.Domain;

namespace Happie.Api.Tests.Infrastructure;

// Feature: dish-recipes, Property 12: For any valid recipe data (summary ≤250 chars, servings 1-25,
// duration as nullable int, list of ≤30 ingredients with valid fields, list of ≤15 instructions with
// valid text, ingredient check states), storing the data and then retrieving it SHALL produce an
// equivalent result.
/// <summary>Property-based tests for recipe mapper round-trip persistence correctness.</summary>
public class RecipeMapperRoundTripPropertyTests
{
    private readonly RecipeSummaryMapper _recipeSummaryMapper = new();
    private readonly IngredientMapper _ingredientMapper = new();
    private readonly CookingInstructionMapper _cookingInstructionMapper = new();
    private readonly IngredientCheckMapper _ingredientCheckMapper = new();

    /// <summary>
    /// For any valid RecipeSummary domain object, mapping to entity via ToEntity and back via
    /// ToModel should produce an equivalent RecipeSummary.
    /// Validates: Requirements 12.1
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ToEntity_ThenToModel_PreservesRecipeSummary()
    {
        return Prop.ForAll(
            RecipeSummaryArb(),
            summary =>
            {
                // Act.
                var entity = _recipeSummaryMapper.ToEntity(summary);
                var roundTripped = _recipeSummaryMapper.ToModel(summary.HouseholdId, entity);

                // Assert.
                return (roundTripped.HouseholdId == summary.HouseholdId)
                    .Label($"HouseholdId mismatch: expected {summary.HouseholdId} but got {roundTripped.HouseholdId}")
                    .And((roundTripped.SavedDishId == summary.SavedDishId)
                        .Label($"SavedDishId mismatch: expected {summary.SavedDishId} but got {roundTripped.SavedDishId}"))
                    .And((roundTripped.Summary == summary.Summary)
                        .Label($"Summary mismatch: expected '{summary.Summary}' but got '{roundTripped.Summary}'"))
                    .And((roundTripped.CookingDurationMinutes == summary.CookingDurationMinutes)
                        .Label($"CookingDurationMinutes mismatch: expected {summary.CookingDurationMinutes} but got {roundTripped.CookingDurationMinutes}"))
                    .And((roundTripped.Servings == summary.Servings)
                        .Label($"Servings mismatch: expected {summary.Servings} but got {roundTripped.Servings}"));
            });
    }

    /// <summary>
    /// For any valid Ingredient domain object, mapping to entity via ToEntity and back via
    /// ToModel should produce an equivalent Ingredient.
    /// Validates: Requirements 12.2
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ToEntity_ThenToModel_PreservesIngredient()
    {
        return Prop.ForAll(
            IngredientArb(),
            ingredient =>
            {
                // Act.
                var entity = _ingredientMapper.ToEntity(ingredient);
                var roundTripped = _ingredientMapper.ToModel(ingredient.HouseholdId, entity);

                // Assert.
                return (roundTripped.Id == ingredient.Id)
                    .Label($"Id mismatch: expected {ingredient.Id} but got {roundTripped.Id}")
                    .And((roundTripped.HouseholdId == ingredient.HouseholdId)
                        .Label($"HouseholdId mismatch: expected {ingredient.HouseholdId} but got {roundTripped.HouseholdId}"))
                    .And((roundTripped.SavedDishId == ingredient.SavedDishId)
                        .Label($"SavedDishId mismatch: expected {ingredient.SavedDishId} but got {roundTripped.SavedDishId}"))
                    .And((roundTripped.Amount == ingredient.Amount)
                        .Label($"Amount mismatch: expected {ingredient.Amount} but got {roundTripped.Amount}"))
                    .And((roundTripped.Unit == ingredient.Unit)
                        .Label($"Unit mismatch: expected {ingredient.Unit} but got {roundTripped.Unit}"))
                    .And((roundTripped.Name == ingredient.Name)
                        .Label($"Name mismatch: expected '{ingredient.Name}' but got '{roundTripped.Name}'"))
                    .And((roundTripped.SortOrder == ingredient.SortOrder)
                        .Label($"SortOrder mismatch: expected {ingredient.SortOrder} but got {roundTripped.SortOrder}"));
            });
    }

    /// <summary>
    /// For any valid CookingInstruction domain object, mapping to entity via ToEntity and back
    /// via ToModel should produce an equivalent CookingInstruction.
    /// Validates: Requirements 12.3
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ToEntity_ThenToModel_PreservesCookingInstruction()
    {
        return Prop.ForAll(
            CookingInstructionArb(),
            instruction =>
            {
                // Act.
                var entity = _cookingInstructionMapper.ToEntity(instruction);
                var roundTripped = _cookingInstructionMapper.ToModel(instruction.HouseholdId, entity);

                // Assert.
                return (roundTripped.Id == instruction.Id)
                    .Label($"Id mismatch: expected {instruction.Id} but got {roundTripped.Id}")
                    .And((roundTripped.HouseholdId == instruction.HouseholdId)
                        .Label($"HouseholdId mismatch: expected {instruction.HouseholdId} but got {roundTripped.HouseholdId}"))
                    .And((roundTripped.SavedDishId == instruction.SavedDishId)
                        .Label($"SavedDishId mismatch: expected {instruction.SavedDishId} but got {roundTripped.SavedDishId}"))
                    .And((roundTripped.Text == instruction.Text)
                        .Label($"Text mismatch: expected '{instruction.Text}' but got '{roundTripped.Text}'"))
                    .And((roundTripped.SortOrder == instruction.SortOrder)
                        .Label($"SortOrder mismatch: expected {instruction.SortOrder} but got {roundTripped.SortOrder}"));
            });
    }

    /// <summary>
    /// For any valid IngredientCheck domain object, mapping to entity via ToEntity and back via
    /// ToModel should produce an equivalent IngredientCheck.
    /// Validates: Requirements 12.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ToEntity_ThenToModel_PreservesIngredientCheck()
    {
        return Prop.ForAll(
            IngredientCheckArb(),
            check =>
            {
                // Act.
                var entity = _ingredientCheckMapper.ToEntity(check);
                var roundTripped = _ingredientCheckMapper.ToModel(check.HouseholdId, entity);

                // Assert.
                return (roundTripped.HouseholdId == check.HouseholdId)
                    .Label($"HouseholdId mismatch: expected {check.HouseholdId} but got {roundTripped.HouseholdId}")
                    .And((roundTripped.SavedDishId == check.SavedDishId)
                        .Label($"SavedDishId mismatch: expected {check.SavedDishId} but got {roundTripped.SavedDishId}"))
                    .And((roundTripped.IngredientId == check.IngredientId)
                        .Label($"IngredientId mismatch: expected {check.IngredientId} but got {roundTripped.IngredientId}"))
                    .And((roundTripped.IsChecked == check.IsChecked)
                        .Label($"IsChecked mismatch: expected {check.IsChecked} but got {roundTripped.IsChecked}"));
            });
    }

    private static Arbitrary<RecipeSummary> RecipeSummaryArb()
    {
        var guidGen = ArbMap.Default.GeneratorFor<Guid>();

        // Printable ASCII characters excluding control characters.
        var printableCharGen = Gen.Choose(32, 126).Select(x => (char)x);

        // Summary is nullable, max 250 chars.
        var summaryGen = Gen.OneOf(
            Gen.Constant<string?>(null),
            Gen.Choose(1, 250)
                .SelectMany(length => Gen.ListOf(printableCharGen, length)
                    .Select(chars => (string?)new string(chars.ToArray()))));

        // CookingDurationMinutes is nullable non-negative integer.
        var durationGen = Gen.OneOf(
            Gen.Constant<int?>(null),
            Gen.Choose(0, 1439).Select(x => (int?)x));

        // Servings is nullable integer between 1 and 25.
        var servingsGen = Gen.OneOf(
            Gen.Constant<int?>(null),
            Gen.Choose(1, 25).Select(x => (int?)x));

        var gen = guidGen.SelectMany(householdId =>
            guidGen.SelectMany(savedDishId =>
                summaryGen.SelectMany(summary =>
                    durationGen.SelectMany(duration =>
                        servingsGen.Select(servings =>
                            new RecipeSummary(householdId, savedDishId, summary, duration, servings))))));

        return Arb.From(gen);
    }

    private static Arbitrary<Ingredient> IngredientArb()
    {
        var guidGen = ArbMap.Default.GeneratorFor<Guid>();

        // Printable ASCII characters excluding control characters.
        var printableCharGen = Gen.Choose(33, 126).Select(x => (char)x);

        // Amount between 0.01 and 9999.
        var amountGen = Gen.Choose(1, 999900)
            .Select(x => x / 100.0);

        // Unit from the UnitOfMeasurement enum.
        var unitGen = Gen.Elements(Enum.GetValues<UnitOfMeasurement>());

        // Name is non-empty, max 100 chars.
        var nameGen = Gen.Choose(1, 100)
            .SelectMany(length => Gen.ListOf(printableCharGen, length)
                .Select(chars => new string(chars.ToArray())));

        // SortOrder between 0 and 29 (max 30 ingredients).
        var sortOrderGen = Gen.Choose(0, 29);

        var gen = guidGen.SelectMany(id =>
            guidGen.SelectMany(householdId =>
                guidGen.SelectMany(savedDishId =>
                    amountGen.SelectMany(amount =>
                        unitGen.SelectMany(unit =>
                            nameGen.SelectMany(name =>
                                sortOrderGen.Select(sortOrder =>
                                    new Ingredient(id, householdId, savedDishId, amount, unit, name, sortOrder))))))));

        return Arb.From(gen);
    }

    private static Arbitrary<CookingInstruction> CookingInstructionArb()
    {
        var guidGen = ArbMap.Default.GeneratorFor<Guid>();

        // Printable ASCII characters excluding control characters.
        var printableCharGen = Gen.Choose(32, 126).Select(x => (char)x);

        // Text is non-empty, max 500 chars.
        var textGen = Gen.Choose(1, 500)
            .SelectMany(length => Gen.ListOf(printableCharGen, length)
                .Select(chars => new string(chars.ToArray())));

        // SortOrder between 0 and 14 (max 15 instructions).
        var sortOrderGen = Gen.Choose(0, 14);

        var gen = guidGen.SelectMany(id =>
            guidGen.SelectMany(householdId =>
                guidGen.SelectMany(savedDishId =>
                    textGen.SelectMany(text =>
                        sortOrderGen.Select(sortOrder =>
                            new CookingInstruction(id, householdId, savedDishId, text, sortOrder))))));

        return Arb.From(gen);
    }

    private static Arbitrary<IngredientCheck> IngredientCheckArb()
    {
        var guidGen = ArbMap.Default.GeneratorFor<Guid>();
        var isCheckedGen = Gen.Elements(true, false);

        var gen = guidGen.SelectMany(householdId =>
            guidGen.SelectMany(savedDishId =>
                guidGen.SelectMany(ingredientId =>
                    isCheckedGen.Select(isChecked =>
                        new IngredientCheck(householdId, savedDishId, ingredientId, isChecked)))));

        return Arb.From(gen);
    }
}
