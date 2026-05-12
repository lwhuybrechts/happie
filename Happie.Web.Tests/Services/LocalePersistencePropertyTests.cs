using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Shared.Domain;

namespace Happie.Web.Tests.Services;

// Feature: happie, Property 21: Locale persistence round-trip
public class LocalePersistencePropertyTests
{
    private static readonly Arbitrary<Locale> LocaleArb =
        Gen.Elements(Locale.En, Locale.Nl).ToArbitrary();

    // Feature: happie, Property 21: Locale persistence round-trip
    [Property(MaxTest = 100)]
    public Property ToCultureCode_ThenToLocale_RoundTrips()
    {
        return Prop.ForAll(
            LocaleArb,
            locale =>
            {
                // Arrange + Act.
                var cultureCode = locale.ToCultureCode();
                var roundTripped = cultureCode.ToLocale();

                // Assert.
                return (roundTripped == locale)
                    .Label($"Expected {locale} but got {roundTripped} after round-trip via '{cultureCode}'");
            });
    }

    // Feature: happie, Property 21: Locale persistence round-trip
    [Property(MaxTest = 100)]
    public Property ToCultureCode_IsNonEmpty_ForAnyLocale()
    {
        return Prop.ForAll(
            LocaleArb,
            locale =>
            {
                // Arrange + Act.
                var cultureCode = locale.ToCultureCode();

                // Assert.
                return (!string.IsNullOrWhiteSpace(cultureCode))
                    .Label($"Expected non-empty culture code for {locale} but got '{cultureCode}'");
            });
    }
}
