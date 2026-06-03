using System.Globalization;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Web.Services;

namespace Happie.Web.Tests.Services;

// Feature: day-plan-redesign, Property 3: Dish relative time formatting
// Feature: day-plan-redesign, Property 4: History timestamp formatting
public class TimeFormatterPropertyTests
{
    // Range covers ~100 years in seconds (fits in int).
    private static readonly Arbitrary<(DateTimeOffset EditedAt, DateTimeOffset Now)> DishTimePairArb =
        Gen.Choose(0, int.MaxValue)
            .SelectMany(nowTicks =>
            {
                var now = DateTimeOffset.UnixEpoch.AddSeconds(nowTicks);
                return Gen.Choose(0, nowTicks)
                    .Select(editedTicks =>
                    {
                        var editedAt = DateTimeOffset.UnixEpoch.AddSeconds(editedTicks);
                        return (EditedAt: editedAt, Now: now);
                    });
            })
            .ToArbitrary();

    private static readonly Arbitrary<(DateTimeOffset ChangedAt, DateTimeOffset Now)> HistoryTimePairArb =
        Gen.Choose(0, int.MaxValue)
            .SelectMany(nowTicks =>
            {
                var now = DateTimeOffset.UnixEpoch.AddSeconds(nowTicks);
                return Gen.Choose(0, nowTicks)
                    .Select(changedTicks =>
                    {
                        var changedAt = DateTimeOffset.UnixEpoch.AddSeconds(changedTicks);
                        return (ChangedAt: changedAt, Now: now);
                    });
            })
            .ToArbitrary();

    // Feature: day-plan-redesign, Property 3: Dish relative time formatting
    // Validates: Requirements 11.5
    [Property(MaxTest = 100)]
    public Property FormatDishTime_MatchesTimeRangeRules()
    {
        return Prop.ForAll(
            DishTimePairArb,
            pair =>
            {
                var (editedAt, now) = pair;
                var result = TimeFormatter.FormatDishTime(editedAt, now);

                // The formatter converts editedAt to local time before formatting.
                var localEditedAt = editedAt.ToLocalTime();
                var elapsed = now - editedAt;

                bool formatCorrect;
                string expectedDescription;

                if (elapsed.TotalSeconds < 60)
                {
                    formatCorrect = result == "just now";
                    expectedDescription = "just now";
                }
                else if (elapsed.TotalMinutes < 60)
                {
                    var expectedMinutes = (int)elapsed.TotalMinutes;
                    formatCorrect = result == $"{expectedMinutes} min ago";
                    expectedDescription = $"{expectedMinutes} min ago";
                }
                else if (elapsed.TotalHours < 3)
                {
                    var expectedHours = (int)elapsed.TotalHours;
                    formatCorrect = result == $"{expectedHours} hours ago";
                    expectedDescription = $"{expectedHours} hours ago";
                }
                else if (localEditedAt.Date == now.Date)
                {
                    var expected = localEditedAt.ToString("HH:mm", CultureInfo.InvariantCulture);
                    formatCorrect = result == expected;
                    expectedDescription = expected;
                }
                else
                {
                    var expected = localEditedAt.ToString("d MMM HH:mm", CultureInfo.InvariantCulture);
                    formatCorrect = result == expected;
                    expectedDescription = expected;
                }

                return formatCorrect.Label(
                    $"Elapsed {elapsed.TotalSeconds:F0}s: expected \"{expectedDescription}\", got \"{result}\"");
            });
    }

    // Feature: day-plan-redesign, Property 4: History timestamp formatting
    // Validates: Requirements 15.4, 15.5, 15.6
    [Property(MaxTest = 100)]
    public Property FormatHistoryTime_MatchesCalendarRules()
    {
        return Prop.ForAll(
            HistoryTimePairArb,
            pair =>
            {
                var (changedAt, now) = pair;
                var result = TimeFormatter.FormatHistoryTime(changedAt, now);

                // The formatter converts changedAt to local time before formatting.
                var localChangedAt = changedAt.ToLocalTime();

                bool formatCorrect;
                string expectedDescription;

                if (localChangedAt.Date == now.Date)
                {
                    var expected = localChangedAt.ToString("HH:mm", CultureInfo.InvariantCulture);
                    formatCorrect = result == expected;
                    expectedDescription = expected;
                }
                else if (localChangedAt.Year == now.Year)
                {
                    var expected = localChangedAt.ToString("d MMM HH:mm", CultureInfo.InvariantCulture);
                    formatCorrect = result == expected;
                    expectedDescription = expected;
                }
                else
                {
                    var expected = localChangedAt.ToString("d MMM yyyy HH:mm", CultureInfo.InvariantCulture);
                    formatCorrect = result == expected;
                    expectedDescription = expected;
                }

                return formatCorrect.Label(
                    $"ChangedAt={changedAt:O}, Now={now:O}: expected \"{expectedDescription}\", got \"{result}\"");
            });
    }
}
