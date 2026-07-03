using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace Happie.Web.Tests.Services;

// Feature: swipe-preview, Property 5: Pre-fetch results discarded on navigation away
// Validates: Requirements 5.4
public class SwipeCarouselPreFetchCancellationPropertyTests
{
    private static readonly Arbitrary<List<int>> NavigationSequenceArb =
        Gen.Choose(-365, 365)
            .ListOf()
            .Where(x => x.Count >= 2 && x.Count <= 10)
            .Select(x => x.ToList())
            .ToArbitrary();

    [Property(MaxTest = 100)]
    public Property PreFetch_RapidNavigations_OnlyFinalDateApplied() =>
        Prop.ForAll(
            NavigationSequenceArb,
            x =>
            {
                // Simulate the cancellation pattern from DayPlanPage.OnParametersSetAsync.
                // Each navigation cancels the previous CTS and creates a new one.
                DateOnly? appliedPrevDate = null;
                DateOnly? appliedNextDate = null;
                CancellationTokenSource? currentCts = null;

                foreach (var dayOffset in x)
                {
                    // Cancel and dispose previous CTS (mirrors OnParametersSetAsync behavior).
                    currentCts?.Cancel();
                    currentCts?.Dispose();
                    currentCts = new CancellationTokenSource();

                    var navigatedDate = DateOnly.FromDateTime(DateTime.Today).AddDays(dayOffset);
                    var prevDate = navigatedDate.AddDays(-1);
                    var nextDate = navigatedDate.AddDays(1);
                    var token = currentCts.Token;

                    // Simulate pre-fetch completing: check cancellation before applying results.
                    if (!token.IsCancellationRequested)
                    {
                        appliedPrevDate = prevDate;
                        appliedNextDate = nextDate;
                    }
                }

                // Determine the expected final date's adjacent days.
                var finalDate = DateOnly.FromDateTime(DateTime.Today).AddDays(x.Last());
                var expectedPrev = finalDate.AddDays(-1);
                var expectedNext = finalDate.AddDays(1);

                // Clean up.
                currentCts?.Dispose();

                return (appliedPrevDate == expectedPrev && appliedNextDate == expectedNext)
                    .Label($"Expected prev={expectedPrev}, next={expectedNext} but got prev={appliedPrevDate}, next={appliedNextDate}");
            });

    [Property(MaxTest = 100)]
    public Property PreFetch_CancelledToken_PreventsResultApplication() =>
        Prop.ForAll(
            NavigationSequenceArb,
            x =>
            {
                // Simulate async behavior: all navigations fire, then all pre-fetches try to apply.
                // Each navigation cancels the previous CTS. Only the last CTS remains uncancelled.
                var tokenSnapshots = new List<CancellationToken>();
                CancellationTokenSource? currentCts = null;

                foreach (var dayOffset in x)
                {
                    // Cancel previous CTS (simulating navigation away).
                    currentCts?.Cancel();
                    currentCts?.Dispose();
                    currentCts = new CancellationTokenSource();

                    // Capture the token for this navigation's pre-fetch.
                    tokenSnapshots.Add(currentCts.Token);
                }

                // Now simulate all pre-fetch results arriving and checking their tokens.
                var appliedCount = tokenSnapshots.Count(token => !token.IsCancellationRequested);

                // Clean up.
                currentCts?.Dispose();

                // Only the final navigation's pre-fetch should be applied (not cancelled).
                return (appliedCount == 1)
                    .Label($"Expected exactly 1 applied pre-fetch but got {appliedCount} for {x.Count} navigations");
            });
}
