using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Web.Services;

namespace Happie.Web.Tests.Services;

// Feature: happie, Property 29 (supplementary): Calendar grid layout invariants.
public class CalendarGridServicePropertyTests
{
    private static readonly Arbitrary<DateOnly> MonthArb =
        Gen.Choose(2020, 2035)
            .SelectMany(x => Gen.Choose(1, 12).Select(y => new DateOnly(x, y, 1)))
            .ToArbitrary();

    // Feature: happie, Property 29 (supplementary): Calendar grid always starts on Monday and ends on Sunday.
    [Property(MaxTest = 100)]
    public Property GetVisibleDates_AnyMonth_AlwaysStartsOnMondayAndEndsOnSunday()
    {
        return Prop.ForAll(
            MonthArb,
            viewedMonth =>
            {
                var dates = CalendarGridService.GetVisibleDates(viewedMonth);

                return (dates[0].DayOfWeek == DayOfWeek.Monday && dates[^1].DayOfWeek == DayOfWeek.Sunday)
                    .Label($"Expected Monday..Sunday, got {dates[0].DayOfWeek}..{dates[^1].DayOfWeek} for {viewedMonth}");
            });
    }

    // Feature: happie, Property 29 (supplementary): Calendar grid date count is always a multiple of 7.
    [Property(MaxTest = 100)]
    public Property GetVisibleDates_AnyMonth_CountIsDivisibleBySeven()
    {
        return Prop.ForAll(
            MonthArb,
            viewedMonth =>
            {
                var dates = CalendarGridService.GetVisibleDates(viewedMonth);

                return (dates.Count % 7 == 0)
                    .Label($"Expected multiple of 7, got {dates.Count} for {viewedMonth}");
            });
    }

    // Feature: happie, Property 29 (supplementary): Calendar grid contains all days of the viewed month.
    [Property(MaxTest = 100)]
    public Property GetVisibleDates_AnyMonth_ContainsAllDaysOfMonth()
    {
        return Prop.ForAll(
            MonthArb,
            viewedMonth =>
            {
                var dates = CalendarGridService.GetVisibleDates(viewedMonth);
                var daysInMonth = DateTime.DaysInMonth(viewedMonth.Year, viewedMonth.Month);

                var allMonthDaysPresent = true;
                for (var day = 1; day <= daysInMonth; day++)
                {
                    if (!dates.Contains(new DateOnly(viewedMonth.Year, viewedMonth.Month, day)))
                    {
                        allMonthDaysPresent = false;
                        break;
                    }
                }

                return allMonthDaysPresent
                    .Label($"Not all days of {viewedMonth:yyyy-MM} are present in the grid");
            });
    }

    // Feature: happie, Property 29 (supplementary): Calendar grid dates are consecutive.
    [Property(MaxTest = 100)]
    public Property GetVisibleDates_AnyMonth_DatesAreConsecutive()
    {
        return Prop.ForAll(
            MonthArb,
            viewedMonth =>
            {
                var dates = CalendarGridService.GetVisibleDates(viewedMonth);

                var consecutive = true;
                for (var i = 1; i < dates.Count; i++)
                {
                    if (dates[i] != dates[i - 1].AddDays(1))
                    {
                        consecutive = false;
                        break;
                    }
                }

                return consecutive
                    .Label($"Dates are not consecutive for {viewedMonth}");
            });
    }
}
