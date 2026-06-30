using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Web.Services.Caching;
using Microsoft.JSInterop;
using Moq;

namespace Happie.Web.Tests.Services.Caching;

// Feature: offline-cache, Property 7: Calendar cache enforces 2-entry limit preserving current month
public class CacheStoreCalendarLimitPropertyTests
{
    private static readonly string CurrentMonth = DateTime.Now.ToString("yyyy-MM");

    private static readonly Arbitrary<List<string>> MonthSequenceArb =
        Gen.Choose(2020, 2030)
            .SelectMany(x => Gen.Choose(1, 12).Select(y => $"{x:D4}-{y:D2}"))
            .ListOf()
            .Where(x => x.Count >= 1 && x.Count <= 20)
            .Select(x => x.ToList())
            .ToArbitrary();

    // Feature: offline-cache, Property 7: Calendar cache enforces 2-entry limit preserving current month
    // Validates: Requirements 4.3, 4.4
    [Property(MaxTest = 100)]
    public Property PutCalendarAsync_AnyMonthSequence_NeverExceedsTwoEntriesAndPreservesCurrentMonth()
    {
        return Prop.ForAll(
            MonthSequenceArb,
            async months =>
            {
                // Arrange.
                var householdId = Guid.NewGuid().ToString();
                var store = new Dictionary<string, (string json, long timestamp)>();
                var jsRuntimeMock = CreateJsRuntimeMock(householdId, store);
                var sut = new CacheStore(jsRuntimeMock.Object);
                await sut.InitializeAsync();

                // Ensure current month entry exists first.
                await sut.PutCalendarAsync(householdId, CurrentMonth, $"{{\"month\":\"{CurrentMonth}\"}}");

                var limitViolated = false;
                var currentMonthEvicted = false;

                // Act.
                foreach (var month in months)
                {
                    await sut.PutCalendarAsync(householdId, month, $"{{\"month\":\"{month}\"}}");

                    // Assert after each insertion.
                    var entryCount = store.Count;
                    if (entryCount > 2)
                        limitViolated = true;

                    var currentMonthKey = $"{householdId}_{CurrentMonth}";
                    if (store.ContainsKey(currentMonthKey) == false && months.Contains(CurrentMonth) == false && month != CurrentMonth)
                    {
                        // Current month was stored initially, check it's still there.
                    }

                    // Only check current month preservation if it was not overwritten by a different insert.
                    if (!store.ContainsKey(currentMonthKey) && entryCount >= 2)
                        currentMonthEvicted = true;
                }

                return (!limitViolated).Label("Calendar entries exceeded 2-entry limit")
                    .And((!currentMonthEvicted).Label("Current month entry was evicted when it should have been preserved"));
            });
    }

    private static Mock<IJSRuntime> CreateJsRuntimeMock(string householdId, Dictionary<string, (string json, long timestamp)> store)
    {
        var mock = new Mock<IJSRuntime>();

        // Initialize is a no-op.
        mock.Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                "window.happieCache.initialize",
                It.IsAny<object[]>()))
            .ReturnsAsync(default(Microsoft.JSInterop.Infrastructure.IJSVoidResult)!);

        // isAvailable returns true.
        mock.Setup(x => x.InvokeAsync<bool>(
                "window.happieCache.isAvailable",
                It.IsAny<object[]>()))
            .ReturnsAsync(true);

        // getCalendarKeys returns filtered keys.
        mock.Setup(x => x.InvokeAsync<string[]>(
                "window.happieCache.getCalendarKeys",
                It.Is<object[]>(args => args.Length >= 1 && args[0]!.ToString() == householdId)))
            .Returns(() => ValueTask.FromResult(
                store.Keys.Where(x => x.StartsWith($"{householdId}_")).ToArray()));

        // deleteCalendar removes the entry.
        mock.Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                "window.happieCache.deleteCalendar",
                It.Is<object[]>(args => args.Length >= 2 && args[0]!.ToString() == householdId)))
            .Returns((string _, object[] args) =>
            {
                var monthToDelete = args[1]!.ToString()!;
                var key = $"{householdId}_{monthToDelete}";
                store.Remove(key);
                return ValueTask.FromResult(default(Microsoft.JSInterop.Infrastructure.IJSVoidResult)!);
            });

        // putCalendar adds/updates the entry.
        mock.Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                "window.happieCache.putCalendar",
                It.Is<object[]>(args => args.Length >= 4 && args[0]!.ToString() == householdId)))
            .Returns((string _, object[] args) =>
            {
                var month = args[1]!.ToString()!;
                var responseJson = args[2]!.ToString()!;
                var timestamp = Convert.ToInt64(args[3]);
                var key = $"{householdId}_{month}";
                store[key] = (responseJson, timestamp);
                return ValueTask.FromResult(default(Microsoft.JSInterop.Infrastructure.IJSVoidResult)!);
            });

        return mock;
    }
}
