using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using System.Xml.Linq;

namespace Happie.Web.Tests.Resources;

// Feature: happie, Property 20: All translation keys exist in both locales
public class AppStringsTranslationPropertyTests
{
    private static readonly string ResourcesPath = Path.Combine(AppContext.BaseDirectory, "Resources");

    [Fact]
    public void AllKeys_ExistInBothLocales()
    {
        // Arrange.
        var englishKeys = LoadKeys("AppStrings.en.resx");
        var dutchKeys = LoadKeys("AppStrings.nl.resx");

        // Act.
        var missingInDutch = englishKeys.Except(dutchKeys).ToList();
        var missingInEnglish = dutchKeys.Except(englishKeys).ToList();

        // Assert.
        Assert.Empty(missingInDutch);
        Assert.Empty(missingInEnglish);
    }

    // Feature: happie, Property 20: All translation keys exist in both locales
    [Property(MaxTest = 100)]
    public Property AnyEnglishKey_ExistsInDutch()
    {
        // Arrange.
        var englishKeys = LoadKeys("AppStrings.en.resx").ToList();
        var dutchKeys = LoadKeys("AppStrings.nl.resx");

        return Prop.ForAll(
            Gen.Elements(englishKeys.ToArray()).ToArbitrary(),
            key =>
            {
                // Act + Assert.
                return dutchKeys.Contains(key)
                    .Label($"Key '{key}' present in English but missing in Dutch");
            });
    }

    // Feature: happie, Property 20: All translation keys exist in both locales
    [Property(MaxTest = 100)]
    public Property AnyDutchKey_ExistsInEnglish()
    {
        // Arrange.
        var dutchKeys = LoadKeys("AppStrings.nl.resx").ToList();
        var englishKeys = LoadKeys("AppStrings.en.resx");

        return Prop.ForAll(
            Gen.Elements(dutchKeys.ToArray()).ToArbitrary(),
            key =>
            {
                // Act + Assert.
                return englishKeys.Contains(key)
                    .Label($"Key '{key}' present in Dutch but missing in English");
            });
    }

    private static IReadOnlySet<string> LoadKeys(string resxFileName)
    {
        var fullPath = Path.Combine(ResourcesPath, resxFileName);
        var document = XDocument.Load(fullPath);
        return document
            .Descendants("data")
            .Select(x => x.Attribute("name")?.Value)
            .Where(x => x != null)
            .Select(x => x!)
            .ToHashSet();
    }
}
