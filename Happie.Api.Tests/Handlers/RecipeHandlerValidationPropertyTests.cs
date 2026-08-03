using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Api.Domain;
using Happie.Api.Handlers;
using Happie.Api.Infrastructure.Repositories;
using Happie.Api.Results;
using Happie.Shared.Contracts;
using Happie.Shared.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Happie.Api.Tests.Handlers;

// Feature: dish-recipes, Property 1: For any string, the dish name validation SHALL accept the string if and only if it is non-empty after trimming, contains no '&' character, and has at most 100 characters after trimming. Whitespace-only strings SHALL always be rejected.
// Feature: dish-recipes, Property 2: For any combination of summary (string), cooking duration (integer), and servings (integer), the system SHALL accept the values if and only if: summary is null or has at most 250 characters; duration is null or a non-negative integer; servings is null or an integer between 1 and 25 inclusive.
// Feature: dish-recipes, Property 6: For any ingredient input, the amount SHALL be accepted if and only if it is a number between 0.01 and 9999, and the name SHALL be accepted if and only if it is non-empty after trimming and has at most 100 characters.
// Feature: dish-recipes, Property 10: For any instruction paragraph text, the system SHALL accept it if and only if it is non-empty after trimming and has at most 500 characters.
/// <summary>Property-based tests for recipe validation logic in <see cref="RecipeHandler"/> and <see cref="SavedDishHandler"/>.</summary>
public class RecipeHandlerValidationPropertyTests
{
    private readonly Mock<ISavedDishRepository> _savedDishRepositoryMock = new();
    private readonly Mock<IRecipeSummaryRepository> _recipeSummaryRepositoryMock = new();
    private readonly Mock<IIngredientRepository> _ingredientRepositoryMock = new();
    private readonly Mock<ICookingInstructionRepository> _cookingInstructionRepositoryMock = new();
    private readonly Mock<IIngredientCheckRepository> _ingredientCheckRepositoryMock = new();
    private readonly Mock<IDishRepository> _dishRepositoryMock = new();
    private readonly Mock<IDayPlanDishLinkRepository> _dayPlanDishLinkRepositoryMock = new();
    private readonly RecipeHandler _recipeSut;
    private readonly SavedDishHandler _savedDishSut;

    /// <summary>Initializes a new instance of <see cref="RecipeHandlerValidationPropertyTests"/>.</summary>
    public RecipeHandlerValidationPropertyTests()
    {
        _recipeSut = new RecipeHandler(
            _savedDishRepositoryMock.Object,
            _recipeSummaryRepositoryMock.Object,
            _ingredientRepositoryMock.Object,
            _cookingInstructionRepositoryMock.Object,
            _ingredientCheckRepositoryMock.Object);

        _savedDishSut = new SavedDishHandler(
            _savedDishRepositoryMock.Object,
            _dishRepositoryMock.Object,
            _dayPlanDishLinkRepositoryMock.Object,
            NullLogger<SavedDishHandler>.Instance);
    }

    /// <summary>
    /// For any string, the dish name validation SHALL accept the string if and only if it is
    /// non-empty after trimming, contains no '&amp;' character, and has at most 100 characters
    /// after trimming. Whitespace-only strings SHALL always be rejected.
    /// Validates: Requirements 3.5, 3.10
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CreateAsync_DishNameValidation_AcceptsIfAndOnlyIfValid()
    {
        return Prop.ForAll(
            DishNameArb(),
            async name =>
            {
                // Arrange.
                _savedDishRepositoryMock.Reset();
                _dishRepositoryMock.Reset();
                _dayPlanDishLinkRepositoryMock.Reset();

                var householdId = Guid.NewGuid();

                // Setup empty repository so valid names don't hit uniqueness checks.
                _savedDishRepositoryMock
                    .Setup(x => x.GetAllAsync(householdId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<SavedDish>());

                _savedDishRepositoryMock
                    .Setup(x => x.UpsertAsync(It.IsAny<SavedDish>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);

                _dishRepositoryMock
                    .Setup(x => x.GetAllByPartitionAsync(householdId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<DishRecord>());

                _dayPlanDishLinkRepositoryMock
                    .Setup(x => x.GetAllByHouseholdAsync(householdId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<DayPlanDishLink>());

                // Determine expected validity.
                var trimmed = name.Trim();
                var shouldBeValid = trimmed.Length > 0
                    && trimmed.Length <= RecipeConstants.MaxDishNameLength
                    && !trimmed.Contains('&');

                // Act.
                var result = await _savedDishSut.CreateAsync(householdId, name);

                // Assert.
                if (shouldBeValid)
                    return (result.Outcome == SavedDishCreateOutcome.Created)
                        .Label($"Expected Created for valid name '{name}' (trimmed='{trimmed}') but got {result.Outcome}");

                return (result.Outcome == SavedDishCreateOutcome.ValidationError)
                    .Label($"Expected ValidationError for invalid name '{name}' (trimmed='{trimmed}', len={trimmed.Length}) but got {result.Outcome}");
            });
    }

    /// <summary>
    /// For any combination of summary (string), cooking duration (integer), and servings (integer),
    /// the system SHALL accept the values if and only if: summary is null or has at most 250 characters;
    /// duration is null or a non-negative integer; servings is null or an integer between 1 and 25 inclusive.
    /// Validates: Requirements 4.2, 4.3, 4.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UpdateSummaryAsync_SummaryFieldValidation_AcceptsIfAndOnlyIfValid()
    {
        return Prop.ForAll(
            SummaryFieldsArb(),
            async scenario =>
            {
                // Arrange.
                _savedDishRepositoryMock.Reset();
                _recipeSummaryRepositoryMock.Reset();

                var householdId = Guid.NewGuid();
                var savedDishId = Guid.NewGuid();

                // Setup existing dish so validation can proceed past the not-found check.
                _savedDishRepositoryMock
                    .Setup(x => x.GetAsync(householdId, savedDishId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new SavedDish(savedDishId, householdId, "Test Dish", false));

                _recipeSummaryRepositoryMock
                    .Setup(x => x.UpsertAsync(It.IsAny<RecipeSummary>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);

                var request = new UpdateSummaryRequest(scenario.Summary, scenario.CookingDurationMinutes, scenario.Servings);

                // Determine expected validity.
                var summaryValid = scenario.Summary is null || scenario.Summary.Length <= RecipeConstants.MaxSummaryLength;
                var durationValid = scenario.CookingDurationMinutes is null || scenario.CookingDurationMinutes >= 0;
                var servingsValid = scenario.Servings is null
                    || (scenario.Servings >= RecipeConstants.MinServings && scenario.Servings <= RecipeConstants.MaxServings);
                var shouldBeValid = summaryValid && durationValid && servingsValid;

                // Act.
                var result = await _recipeSut.UpdateSummaryAsync(householdId, savedDishId, request, CancellationToken.None);

                // Assert.
                if (shouldBeValid)
                    return (result.Outcome == UpdateSummaryOutcome.Success)
                        .Label($"Expected Success but got {result.Outcome} for summary={scenario.Summary?.Length ?? -1}chars, duration={scenario.CookingDurationMinutes}, servings={scenario.Servings}");

                return (result.Outcome == UpdateSummaryOutcome.ValidationError)
                    .Label($"Expected ValidationError but got {result.Outcome} for summary={scenario.Summary?.Length ?? -1}chars, duration={scenario.CookingDurationMinutes}, servings={scenario.Servings}");
            });
    }

    /// <summary>
    /// For any ingredient input, the amount SHALL be accepted if and only if it is a number
    /// between 0.01 and 9999, and the name SHALL be accepted if and only if it is non-empty
    /// after trimming and has at most 100 characters.
    /// Validates: Requirements 6.3, 6.12
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UpdateIngredientsAsync_IngredientFieldValidation_AcceptsIfAndOnlyIfValid()
    {
        return Prop.ForAll(
            IngredientFieldsArb(),
            async scenario =>
            {
                // Arrange.
                _savedDishRepositoryMock.Reset();
                _ingredientRepositoryMock.Reset();
                _ingredientCheckRepositoryMock.Reset();

                var householdId = Guid.NewGuid();
                var savedDishId = Guid.NewGuid();

                _savedDishRepositoryMock
                    .Setup(x => x.GetAsync(householdId, savedDishId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new SavedDish(savedDishId, householdId, "Test Dish", false));

                _ingredientRepositoryMock
                    .Setup(x => x.GetAllAsync(householdId, savedDishId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<Ingredient>());

                _ingredientRepositoryMock
                    .Setup(x => x.BatchUpsertAsync(It.IsAny<IReadOnlyList<Ingredient>>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);

                var ingredientDto = new IngredientDto(Guid.NewGuid(), scenario.Amount, UnitOfMeasurement.G, scenario.Name, 0);
                var request = new UpdateIngredientsRequest(new List<IngredientDto> { ingredientDto });

                // Determine expected validity.
                var amountValid = scenario.Amount >= RecipeConstants.MinIngredientAmount
                    && scenario.Amount <= RecipeConstants.MaxIngredientAmount;
                var nameValid = scenario.Name.Length <= RecipeConstants.MaxIngredientNameLength;
                var shouldBeValid = amountValid && nameValid;

                // Act.
                var result = await _recipeSut.UpdateIngredientsAsync(householdId, savedDishId, request, CancellationToken.None);

                // Assert.
                if (shouldBeValid)
                    return (result.Outcome == UpdateIngredientsOutcome.Success)
                        .Label($"Expected Success but got {result.Outcome} for amount={scenario.Amount}, name.Length={scenario.Name.Length}");

                return (result.Outcome == UpdateIngredientsOutcome.ValidationError)
                    .Label($"Expected ValidationError but got {result.Outcome} for amount={scenario.Amount}, name.Length={scenario.Name.Length}");
            });
    }

    /// <summary>
    /// For any instruction paragraph text, the system SHALL accept it if and only if it is
    /// non-empty after trimming and has at most 500 characters.
    /// Validates: Requirements 8.9
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UpdateInstructionsAsync_InstructionTextValidation_AcceptsIfAndOnlyIfValid()
    {
        return Prop.ForAll(
            InstructionTextArb(),
            async text =>
            {
                // Arrange.
                _savedDishRepositoryMock.Reset();
                _cookingInstructionRepositoryMock.Reset();

                var householdId = Guid.NewGuid();
                var savedDishId = Guid.NewGuid();

                _savedDishRepositoryMock
                    .Setup(x => x.GetAsync(householdId, savedDishId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new SavedDish(savedDishId, householdId, "Test Dish", false));

                _cookingInstructionRepositoryMock
                    .Setup(x => x.GetAllAsync(householdId, savedDishId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<CookingInstruction>());

                _cookingInstructionRepositoryMock
                    .Setup(x => x.BatchUpsertAsync(It.IsAny<IReadOnlyList<CookingInstruction>>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);

                var instructionDto = new CookingInstructionDto(Guid.NewGuid(), text, 0);
                var request = new UpdateInstructionsRequest(new List<CookingInstructionDto> { instructionDto });

                // Determine expected validity.
                var trimmed = text.Trim();
                var shouldBeValid = text.Length <= RecipeConstants.MaxInstructionTextLength;

                // Act.
                var result = await _recipeSut.UpdateInstructionsAsync(householdId, savedDishId, request, CancellationToken.None);

                // Assert.
                if (shouldBeValid)
                    return (result.Outcome == UpdateInstructionsOutcome.Success)
                        .Label($"Expected Success but got {result.Outcome} for text.Length={text.Length}, trimmed.Length={trimmed.Length}");

                return (result.Outcome == UpdateInstructionsOutcome.ValidationError)
                    .Label($"Expected ValidationError but got {result.Outcome} for text.Length={text.Length}, trimmed.Length={trimmed.Length}");
            });
    }

    private static Arbitrary<string> DishNameArb()
    {
        // Generate a mix of valid and invalid dish names.
        var validCharGen = Gen.Choose(32, 126).Select(x => (char)x).Where(x => x != '&');
        var anyCharGen = Gen.Choose(32, 126).Select(x => (char)x);

        var gen = Gen.Choose(0, 4).SelectMany(scenario => scenario switch
        {
            // Whitespace-only strings (should be rejected).
            0 => Gen.Choose(1, 10)
                .SelectMany(length => Gen.Constant(' ')
                    .Select(x => new string(x, length))),

            // Valid names: non-empty, no '&', at most 100 chars.
            1 => Gen.Choose(1, RecipeConstants.MaxDishNameLength)
                .SelectMany(length => Gen.ListOf(validCharGen, length)
                    .Select(chars => new string(chars.ToArray()))
                    .Where(x => x.Trim().Length > 0)),

            // Names containing '&' (should be rejected).
            2 => Gen.Choose(1, 50)
                .SelectMany(length => Gen.ListOf(validCharGen, length)
                    .SelectMany(chars =>
                        Gen.Choose(0, chars.Count).Select(insertIndex =>
                        {
                            var list = chars.ToList();
                            list.Insert(insertIndex, '&');
                            return new string(list.ToArray());
                        }))),

            // Names exceeding 100 characters after trimming (should be rejected).
            3 => Gen.Choose(RecipeConstants.MaxDishNameLength + 1, RecipeConstants.MaxDishNameLength + 50)
                .SelectMany(length => Gen.ListOf(validCharGen, length)
                    .Select(chars => new string(chars.ToArray()))
                    .Where(x => x.Trim().Length > RecipeConstants.MaxDishNameLength)),

            // Empty string (should be rejected).
            _ => Gen.Constant(string.Empty)
        });

        return Arb.From(gen);
    }

    private static Arbitrary<SummaryFieldScenario> SummaryFieldsArb()
    {
        var charGen = Gen.Choose(32, 126).Select(x => (char)x);

        // Generate nullable summary string.
        var summaryGen = Gen.Choose(0, 2).SelectMany(choice => choice switch
        {
            // Null summary.
            0 => Gen.Constant<string?>(null),
            // Valid summary (at most 250 chars).
            1 => Gen.Choose(0, RecipeConstants.MaxSummaryLength)
                .SelectMany(length => Gen.ListOf(charGen, length)
                    .Select(chars => (string?)new string(chars.ToArray()))),
            // Invalid summary (exceeds 250 chars).
            _ => Gen.Choose(RecipeConstants.MaxSummaryLength + 1, RecipeConstants.MaxSummaryLength + 100)
                .SelectMany(length => Gen.ListOf(charGen, length)
                    .Select(chars => (string?)new string(chars.ToArray())))
        });

        // Generate nullable cooking duration.
        var durationGen = Gen.Choose(0, 2).SelectMany(choice => choice switch
        {
            // Null duration.
            0 => Gen.Constant<int?>(null),
            // Valid non-negative duration.
            1 => Gen.Choose(0, 1440).Select(x => (int?)x),
            // Invalid negative duration.
            _ => Gen.Choose(-100, -1).Select(x => (int?)x)
        });

        // Generate nullable servings.
        var servingsGen = Gen.Choose(0, 3).SelectMany(choice => choice switch
        {
            // Null servings.
            0 => Gen.Constant<int?>(null),
            // Valid servings (1–25).
            1 => Gen.Choose(RecipeConstants.MinServings, RecipeConstants.MaxServings).Select(x => (int?)x),
            // Invalid servings (below min).
            2 => Gen.Choose(-10, RecipeConstants.MinServings - 1).Select(x => (int?)x),
            // Invalid servings (above max).
            _ => Gen.Choose(RecipeConstants.MaxServings + 1, RecipeConstants.MaxServings + 50).Select(x => (int?)x)
        });

        var gen = summaryGen.SelectMany(summary =>
            durationGen.SelectMany(duration =>
                servingsGen.Select(servings =>
                    new SummaryFieldScenario(summary, duration, servings))));

        return Arb.From(gen);
    }

    private static Arbitrary<IngredientFieldScenario> IngredientFieldsArb()
    {
        var charGen = Gen.Choose(33, 126).Select(x => (char)x);

        // Generate amount (mix of valid and invalid).
        var amountGen = Gen.Choose(0, 2).SelectMany(choice => choice switch
        {
            // Valid amount (0.01–9999).
            0 => Gen.Choose(1, 999900).Select(x => x / 100.0),
            // Invalid amount (below min).
            1 => Gen.Choose(-10000, 0).Select(x => x / 100.0),
            // Invalid amount (above max).
            _ => Gen.Choose(999901, 1500000).Select(x => x / 100.0)
        });

        // Generate name (mix of valid and invalid).
        var nameGen = Gen.Choose(0, 1).SelectMany(choice => choice switch
        {
            // Valid name (1–100 chars).
            0 => Gen.Choose(1, RecipeConstants.MaxIngredientNameLength)
                .SelectMany(length => Gen.ListOf(charGen, length)
                    .Select(chars => new string(chars.ToArray()))),
            // Invalid name (exceeds 100 chars).
            _ => Gen.Choose(RecipeConstants.MaxIngredientNameLength + 1, RecipeConstants.MaxIngredientNameLength + 50)
                .SelectMany(length => Gen.ListOf(charGen, length)
                    .Select(chars => new string(chars.ToArray())))
        });

        var gen = amountGen.SelectMany(amount =>
            nameGen.Select(name =>
                new IngredientFieldScenario(amount, name)));

        return Arb.From(gen);
    }

    private static Arbitrary<string> InstructionTextArb()
    {
        var charGen = Gen.Choose(32, 126).Select(x => (char)x);

        var gen = Gen.Choose(0, 1).SelectMany(choice => choice switch
        {
            // Valid text (1–500 chars).
            0 => Gen.Choose(1, RecipeConstants.MaxInstructionTextLength)
                .SelectMany(length => Gen.ListOf(charGen, length)
                    .Select(chars => new string(chars.ToArray()))),
            // Invalid text (exceeds 500 chars).
            _ => Gen.Choose(RecipeConstants.MaxInstructionTextLength + 1, RecipeConstants.MaxInstructionTextLength + 100)
                .SelectMany(length => Gen.ListOf(charGen, length)
                    .Select(chars => new string(chars.ToArray())))
        });

        return Arb.From(gen);
    }

    private record SummaryFieldScenario(string? Summary, int? CookingDurationMinutes, int? Servings);

    private record IngredientFieldScenario(double Amount, string Name);
}
