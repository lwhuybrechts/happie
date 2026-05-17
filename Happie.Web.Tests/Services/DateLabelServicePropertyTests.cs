using System.Globalization;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Web.Services;

namespace Happie.Web.Tests.Services;

// Feature: day-plan-redesign, Property 1: Date label contextual title correctness
// Feature: day-plan-redesign, Property 2: Date label formatted date uses locale-aware month abbreviation
public class DateLabelServicePropertyTests
{
    private static readonly CultureInfo EnglishCulture = CultureInfo.GetCultureInfo("en");
    private static readonly CultureInfo DutchCulture = CultureInfo.GetCultureInfo("nl");

    private static readonly Arbitrary<DateOnly> DateOnlyArb =
        Gen.Choose(1, 3_652_058)
            .Select(x => DateOnly.FromDayNumber(x))
            .ToArbitrary();

    private static readonly Arbitrary<(DateOnly ViewedDate, DateOnly Today)> DatePairArb =
        DateOnlyArb.Generator
            .SelectMany(x => DateOnlyArb.Generator.Select(y => (ViewedDate: x, Today: y)))
            .ToArbitrary();

    private static readonly Arbitrary<CultureInfo> CultureArb =
        Gen.Elements(EnglishCulture, DutchCulture)
            .ToArbitrary();

    // Feature: day-plan-redesign, Property 1: Date label contextual title correctness
    // Validates: Requirements 10.4, 10.5, 10.6, 10.7, 10.8
    [Property(MaxTest = 100)]
    public Property GetLabel_ContextualTitle_MatchesOffsetRules()
    {
        return Prop.ForAll(
            DatePairArb,
            pair =>
            {
                var (viewedDate, today) = pair;
                var offset = viewedDate.DayNumber - today.DayNumber;
                var result = DateLabelService.GetLabel(viewedDate, today, EnglishCulture);

                bool titleCorrect;

                if (offset == 0)
                    titleCorrect = result.Title == "Today";
                else if (offset == -1)
                    titleCorrect = result.Title == "Yesterday";
                else if (offset == 1)
                    titleCorrect = result.Title == "Tomorrow";
                else if (offset >= 2 && offset <= 6)
                    titleCorrect = result.Title == viewedDate.ToString("dddd", EnglishCulture);
                else
                    titleCorrect = result.Title == null;

                return titleCorrect.Label(
                    $"Offset {offset}: expected title rule to hold, got Title=\"{result.Title}\"");
            });
    }

    // Feature: day-plan-redesign, Property 2: Date label formatted date uses locale-aware month abbreviation
    // Validates: Requirements 10.9
    [Property(MaxTest = 100)]
    public Property GetLabel_FormattedDate_ContainsLocaleAwareMonthAbbreviation()
    {
        return Prop.ForAll(
            DateOnlyArb,
            CultureArb,
            (viewedDate, culture) =>
            {
                var result = DateLabelService.GetLabel(viewedDate, viewedDate, culture);
                var expectedMonth = viewedDate.ToString("MMM", culture);

                return result.FormattedDate.Contains(expectedMonth).Label(
                    $"Expected formatted date \"{result.FormattedDate}\" to contain locale month \"{expectedMonth}\" for culture {culture.Name}");
            });
    }
}
