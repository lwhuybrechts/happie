using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Shared.Contracts;
using Happie.Shared.Domain;
using Happie.Web.Services;

namespace Happie.Web.Tests.Services;

// Feature: dish-autocomplete.
public class DishAutocompleteEnginePropertyTests
{
    // Arbitrary that generates a non-empty list of SavedDishDto with non-empty descriptions.
    private static readonly Arbitrary<List<SavedDishDto>> SavedDishListArb =
        ArbMap.Default.ArbFor<NonEmptyString>()
            .Generator
            .Where(x => !x.Get.Contains(" & "))
            .Select(x => new SavedDishDto(Guid.NewGuid(), x.Get))
            .NonEmptyListOf()
            .Select(x => x.ToList())
            .ToArbitrary();

    // Generates a (savedDishList, prefix) pair where prefix is a proper prefix of one dish's description.
    private static readonly Arbitrary<(List<SavedDishDto> Dishes, string Prefix)> ArbMatchingPair =
        SavedDishListArb.Generator
            .SelectMany(dishes =>
            {
                var validDishes = dishes
                    .Where(x => !string.IsNullOrEmpty(x.Description) && x.Description.Length > 1)
                    .ToList();

                if (validDishes.Count == 0)
                    return Gen.Constant((dishes, "A"));

                return Gen.Elements(validDishes.ToArray())
                    .SelectMany(dish =>
                        Gen.Choose(1, dish.Description.Length - 1)
                            .Select(prefixLength => (dishes, dish.Description[..prefixLength])));
            })
            .ToArbitrary();

    // Feature: dish-autocomplete, Property 1: Prefix match selects the first sorted match.
    // Validates: Requirements 1.1, 1.2.
    [Property(MaxTest = 100)]
    public Property GetSuggestion_Match_IsFirstSortedPrefixMatch()
    {
        return Prop.ForAll(
            ArbMatchingPair,
            pair =>
            {
                var (dishes, prefix) = pair;
                var result = DishAutocompleteEngine.GetSuggestion(prefix, dishes);

                if (result is null)
                    return true.Label("No match returned — vacuously true.");

                var matchedDish = prefix + result;

                // The matched dish must start with the active segment (case-insensitive ordinal).
                var startsWithPrefix = matchedDish.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

                // No other matching dish should sort before the selected match.
                var allMatches = dishes
                    .Where(x => !string.IsNullOrEmpty(x.Description))
                    .Where(x => x.Description.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    .Where(x => !x.Description.Equals(prefix, StringComparison.OrdinalIgnoreCase))
                    .Select(x => x.Description)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var isFirstSorted = allMatches.Count == 0
                    || string.Compare(matchedDish, allMatches[0], StringComparison.OrdinalIgnoreCase) <= 0;

                return (startsWithPrefix && isFirstSorted)
                    .Label($"Prefix: '{prefix}', Matched: '{matchedDish}', FirstSorted: '{(allMatches.Count > 0 ? allMatches[0] : "N/A")}'");
            });
    }

    // Feature: dish-autocomplete, Property 2: Non-matching segment returns null.
    // Validates: Requirements 1.3.
    [Property(MaxTest = 100)]
    public Property GetSuggestion_NoMatch_ReturnsNull()
    {
        var arbitrary = GenerateNonMatchingInput().ToArbitrary();

        return Prop.ForAll(arbitrary, input =>
        {
            var (activeSegment, dishes) = input;
            var result = DishAutocompleteEngine.GetSuggestion(activeSegment, dishes);

            return (result == null)
                .Label($"Expected null but got \"{result}\" for segment \"{activeSegment}\" with {dishes.Count} dishes");
        });
    }

    // Feature: dish-autocomplete, Property 3: Exact full match returns null.
    // Validates: Requirements 1.4.
    [Property(MaxTest = 100)]
    public Property GetSuggestion_ExactFullMatch_ReturnsNull()
    {
        var arbitrary = SavedDishListArb.Generator
            .SelectMany(dishes =>
                Gen.Choose(0, dishes.Count - 1)
                    .SelectMany(index =>
                        Gen.Elements(new Func<string, string>[]
                            {
                                s => s,
                                s => s.ToUpperInvariant(),
                                s => s.ToLowerInvariant()
                            })
                            .Select(caseTransform => new
                            {
                                Dishes = dishes,
                                ActiveSegment = caseTransform(dishes[index].Description)
                            })))
            .ToArbitrary();

        return Prop.ForAll(arbitrary, input =>
        {
            var result = DishAutocompleteEngine.GetSuggestion(input.ActiveSegment, input.Dishes);

            return (result == null)
                .Label($"Expected null for exact match '{input.ActiveSegment}' but got '{result}'");
        });
    }

    // Feature: dish-autocomplete, Property 5: Active segment extraction.
    // Validates: Requirements 4.1, 4.2.
    [Property(MaxTest = 100)]
    public Property ExtractActiveSegment_ReturnsTextAfterLastDelimiter()
    {
        var withDelimitersArb = GenerateInputWithDelimiters().ToArbitrary();
        var withoutDelimitersArb = GenerateInputWithoutDelimiters().ToArbitrary();

        var withDelimiters = Prop.ForAll(withDelimitersArb, input =>
        {
            var (inputText, expectedSegment) = input;
            var result = DishAutocompleteEngine.ExtractActiveSegment(inputText);

            return (result == expectedSegment)
                .Label($"With delimiters: expected \"{expectedSegment}\" but got \"{result}\" for input \"{inputText}\"");
        });

        var withoutDelimiters = Prop.ForAll(withoutDelimitersArb, inputText =>
        {
            var result = DishAutocompleteEngine.ExtractActiveSegment(inputText);

            return (result == inputText)
                .Label($"Without delimiters: expected \"{inputText}\" but got \"{result}\"");
        });

        return withDelimiters.And(withoutDelimiters);
    }

    // Feature: dish-autocomplete, Property 4: Suggestion is the untyped remainder.
    // Validates: Requirements 2.4, 2.9.
    [Property(MaxTest = 100)]
    public Property GetSuggestion_Match_ReturnsUntypedRemainder()
    {
        // Generate a dish description (2+ chars) and a proper prefix of it.
        var arbRemainderPair =
            ArbMap.Default.ArbFor<NonEmptyString>()
                .Generator
                .Select(x => x.Get)
                .Where(x => x.Length >= 2 && !x.Contains(" & "))
                .SelectMany(description =>
                    Gen.Choose(1, description.Length - 1)
                        .Select(prefixLength => new
                        {
                            Description = description,
                            ActiveSegment = description[..prefixLength]
                        }))
                .ToArbitrary();

        return Prop.ForAll(arbRemainderPair, input =>
        {
            var dish = new SavedDishDto(Guid.NewGuid(), input.Description);
            var dishes = new List<SavedDishDto> { dish };

            var result = DishAutocompleteEngine.GetSuggestion(input.ActiveSegment, dishes);

            // The result must be the untyped remainder of the dish description.
            var expectedRemainder = input.Description[input.ActiveSegment.Length..];
            var remainderMatches = result == expectedRemainder;

            // activeSegment + result must equal the full dish description.
            var concatenationMatches = result is not null
                && string.Equals(input.ActiveSegment + result, input.Description, StringComparison.OrdinalIgnoreCase);

            return (remainderMatches && concatenationMatches)
                .Label($"Segment: '{input.ActiveSegment}', Expected: '{expectedRemainder}', Got: '{result}'");
        });
    }

    // Feature: dish-autocomplete, Property 6: Accept preserves preceding text.
    // Validates: Requirements 3.1, 4.3.
    [Property(MaxTest = 100)]
    public Property AcceptSuggestion_PreservesPrecedingText()
    {
        // Case 1: input WITH delimiters.
        var withDelimitersArb = GenerateInputWithDelimiters()
            .SelectMany(input =>
                GenerateMatchedDishName()
                    .Select(matchedDishName => new { input.InputText, input.ExpectedSegment, MatchedDishName = matchedDishName }))
            .ToArbitrary();

        // Case 2: input WITHOUT delimiters.
        var withoutDelimitersArb = GenerateInputWithoutDelimiters()
            .SelectMany(inputText =>
                GenerateMatchedDishName()
                    .Select(matchedDishName => new { InputText = inputText, MatchedDishName = matchedDishName }))
            .ToArbitrary();

        var withDelimiters = Prop.ForAll(withDelimitersArb, input =>
        {
            var result = DishAutocompleteEngine.AcceptSuggestion(input.InputText, input.MatchedDishName);

            // Everything up to and including the last delimiter must be preserved.
            var lastDelimiterIndex = input.InputText.LastIndexOf(DishConstants.Delimiter, StringComparison.Ordinal);
            var expectedPrefix = input.InputText[..(lastDelimiterIndex + DishConstants.Delimiter.Length)];
            var expectedResult = expectedPrefix + input.MatchedDishName;

            return (result == expectedResult)
                .Label($"With delimiters: expected \"{expectedResult}\" but got \"{result}\" for input \"{input.InputText}\" + dish \"{input.MatchedDishName}\"");
        });

        var withoutDelimiters = Prop.ForAll(withoutDelimitersArb, input =>
        {
            var result = DishAutocompleteEngine.AcceptSuggestion(input.InputText, input.MatchedDishName);

            return (result == input.MatchedDishName)
                .Label($"Without delimiters: expected \"{input.MatchedDishName}\" but got \"{result}\" for input \"{input.InputText}\"");
        });

        return withDelimiters.And(withoutDelimiters);
    }

    /// <summary>
    /// Generates a non-empty matched dish name that does not contain the delimiter.
    /// </summary>
    private static Gen<string> GenerateMatchedDishName()
    {
        return Gen.Choose(1, 20)
            .SelectMany(length =>
                Gen.Choose('a', 'z').Select(x => (char)x)
                    .ArrayOf(length)
                    .Select(x => new string(x)));
    }

    /// <summary>
    /// Generates an input string composed of 2-4 non-empty segments joined by the delimiter.
    /// Returns the full input and the expected last segment.
    /// </summary>
    private static Gen<(string InputText, string ExpectedSegment)> GenerateInputWithDelimiters()
    {
        // Generate a non-empty segment that does not contain the delimiter.
        var segmentGen = Gen.Choose(1, 15)
            .SelectMany(length =>
                Gen.Choose('a', 'z').Select(x => (char)x)
                    .ArrayOf(length)
                    .Select(x => new string(x)));

        return Gen.Choose(2, 4)
            .SelectMany(count => segmentGen.ArrayOf(count))
            .Select(segments =>
            {
                var inputText = string.Join(DishConstants.Delimiter, segments);
                var expectedSegment = segments[^1];
                return (inputText, expectedSegment);
            });
    }

    /// <summary>
    /// Generates a non-empty input string that does not contain the delimiter.
    /// </summary>
    private static Gen<string> GenerateInputWithoutDelimiters()
    {
        return Gen.Choose(1, 30)
            .SelectMany(length =>
                Gen.Choose('a', 'z').Select(x => (char)x)
                    .ArrayOf(length)
                    .Select(x => new string(x)));
    }

    /// <summary>
    /// Generates a non-empty active segment and a non-empty list of dishes where
    /// no dish description starts with the active segment (case-insensitive ordinal).
    /// </summary>
    private static Gen<(string ActiveSegment, List<SavedDishDto> Dishes)> GenerateNonMatchingInput()
    {
        // Generate a non-empty active segment with a prefix unlikely to match dish descriptions.
        var segmentGen = Gen.Choose(1, 15)
            .SelectMany(length =>
                Gen.Choose('a', 'z').Select(x => (char)x)
                    .ArrayOf(length)
                    .Select(x => "zzq" + new string(x)));

        // Generate dish descriptions that start with a different prefix.
        var dishDescriptionGen = Gen.Choose(1, 15)
            .SelectMany(length =>
                Gen.Choose('a', 'z').Select(x => (char)x)
                    .ArrayOf(length)
                    .Select(x => "aab" + new string(x)));

        var dishListGen = Gen.Choose(1, 10)
            .SelectMany(count =>
                dishDescriptionGen.ArrayOf(count)
                    .Select(x => x.Select(d => new SavedDishDto(Guid.NewGuid(), d)).ToList()));

        return segmentGen.SelectMany(segment =>
            dishListGen
                // Filter out any dishes that happen to start with the segment.
                .Select(dishes => dishes
                    .Where(x => !x.Description.StartsWith(segment, StringComparison.OrdinalIgnoreCase))
                    .ToList())
                .Where(dishes => dishes.Count > 0)
                .Select(dishes => (segment, dishes)));
    }
}
