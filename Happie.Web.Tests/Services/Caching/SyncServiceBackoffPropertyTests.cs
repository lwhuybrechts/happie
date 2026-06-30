using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Web.Services.Caching;

namespace Happie.Web.Tests.Services.Caching;

// Feature: offline-cache, Property 12: Exponential backoff delay formula
public class SyncServiceBackoffPropertyTests
{
    private static readonly Arbitrary<int> RetryAttemptArb =
        Gen.Choose(1, 20)
            .ToArbitrary();

    // Feature: offline-cache, Property 12: Exponential backoff delay formula
    // Validates: Requirements 6.6
    [Property(MaxTest = 100)]
    public Property CalculateBackoffDelay_MatchesExponentialFormula()
    {
        return Prop.ForAll(
            RetryAttemptArb,
            x =>
            {
                var result = SyncService.CalculateBackoffDelay(x);
                var expected = Math.Min((int)Math.Pow(2, x) * 1000, 60000);

                return (result == expected)
                    .Label($"For attempt {x}: expected {expected} ms, got {result} ms");
            });
    }

    // Feature: offline-cache, Property 12: Exponential backoff delay formula
    // Validates: Requirements 6.6
    [Property(MaxTest = 100)]
    public Property CalculateBackoffDelay_NeverExceedsCap()
    {
        return Prop.ForAll(
            RetryAttemptArb,
            x =>
            {
                var result = SyncService.CalculateBackoffDelay(x);

                return (result <= 60000)
                    .Label($"For attempt {x}: result {result} ms exceeds cap of 60000 ms");
            });
    }
}
