using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Api.Handlers;

namespace Happie.Api.Tests.Handlers;

// Feature: dinner-time, Property 3: Notification window decision
/// <summary>
/// Property-based tests for the dinner-time notification window decision.
/// For any combination of (previousDinnerTime, newDinnerTime, currentUtcTime, timezoneOffsetMinutes, date),
/// the notification decision SHALL return "send notification" if and only if:
/// (a) newDinnerTime is not null, AND
/// (b) newDinnerTime differs from previousDinnerTime, AND
/// (c) the naive dinner DateTime (date + newDinnerTime) minus the setter's local time
///     (currentUtcTime + timezoneOffsetMinutes) is in the range (0, 6 hours exclusive).
/// Validates: Requirements 6.1, 6.2, 6.3, 6.4
/// </summary>
public class DayHandlerNotificationWindowPropertyTests
{
    /// <summary>
    /// For any valid inputs, ShouldNotifyDinnerTimeChange returns true if and only if
    /// all three conditions are met: newDinnerTime is not null, differs from previous,
    /// and the dinner time is within 0 to 6 hours from the setter's local time.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ShouldNotifyDinnerTimeChange_ReturnsTrue_IfAndOnlyIfAllConditionsMet()
    {
        return Prop.ForAll(
            NotificationWindowInputArb(),
            input =>
            {
                // Act.
                var result = DayHandler.ShouldNotifyDinnerTimeChange(
                    input.PreviousDinnerTime, input.NewDinnerTime,
                    input.CurrentUtcTime, input.TimezoneOffsetMinutes, input.Date);

                // Compute expected result from the specification.
                var conditionA = input.NewDinnerTime is not null;
                var conditionB = input.NewDinnerTime != input.PreviousDinnerTime;
                var conditionC = false;

                if (conditionA)
                {
                    var setterLocalNow = input.CurrentUtcTime.AddMinutes(input.TimezoneOffsetMinutes);
                    var naiveDinnerDateTime = new DateTime(
                        input.Date.Year, input.Date.Month, input.Date.Day,
                        input.NewDinnerTime!.Value.Hour, input.NewDinnerTime.Value.Minute, 0);
                    var difference = naiveDinnerDateTime - setterLocalNow.DateTime;
                    conditionC = difference > TimeSpan.Zero && difference < TimeSpan.FromHours(6);
                }

                var expected = conditionA && conditionB && conditionC;

                return (result == expected)
                    .Label($"Expected={expected}, Actual={result}, " +
                           $"prev={input.PreviousDinnerTime}, new={input.NewDinnerTime}, " +
                           $"utc={input.CurrentUtcTime:O}, offset={input.TimezoneOffsetMinutes}, date={input.Date}");
            });
    }

    private static Arbitrary<NotificationWindowInput> NotificationWindowInputArb()
    {
        var nullTimeGen = Gen.Constant<TimeOnly?>(null);
        var validTimeGen = Gen.Choose(0, 23)
            .SelectMany(hour => Gen.Choose(0, 59)
                .Select(minute => (TimeOnly?)new TimeOnly(hour, minute)));

        var dinnerTimeGen = Gen.OneOf(nullTimeGen, validTimeGen);

        var utcTimeGen = Gen.Choose(2024, 2026)
            .SelectMany(year => Gen.Choose(1, 12)
                .SelectMany(month => Gen.Choose(1, 28)
                    .SelectMany(day => Gen.Choose(0, 23)
                        .SelectMany(hour => Gen.Choose(0, 59)
                            .Select(minute =>
                                new DateTimeOffset(year, month, day, hour, minute, 0, TimeSpan.Zero))))));

        // Timezone offsets range from UTC-12 (-720) to UTC+14 (+840).
        var offsetGen = Gen.Choose(-720, 840);

        var dateGen = Gen.Choose(2024, 2026)
            .SelectMany(year => Gen.Choose(1, 12)
                .SelectMany(month => Gen.Choose(1, 28)
                    .Select(day => new DateOnly(year, month, day))));

        var gen = dinnerTimeGen
            .SelectMany(previousDinnerTime => dinnerTimeGen
                .SelectMany(newDinnerTime => utcTimeGen
                    .SelectMany(currentUtcTime => offsetGen
                        .SelectMany(offset => dateGen
                            .Select(date => new NotificationWindowInput(
                                previousDinnerTime, newDinnerTime, currentUtcTime, offset, date))))));

        return Arb.From(gen);
    }

    private record NotificationWindowInput(
        TimeOnly? PreviousDinnerTime,
        TimeOnly? NewDinnerTime,
        DateTimeOffset CurrentUtcTime,
        int TimezoneOffsetMinutes,
        DateOnly Date);
}
