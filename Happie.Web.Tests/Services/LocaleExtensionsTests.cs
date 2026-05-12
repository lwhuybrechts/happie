using Happie.Shared.Domain;

namespace Happie.Web.Tests.Services;

public class LocaleExtensionsTests
{
    [Fact]
    public void ToLocale_NullInput_ReturnsNl()
    {
        // Arrange.
        string? input = null;

        // Act.
        var result = input.ToLocale();

        // Assert.
        Assert.Equal(Locale.Nl, result);
    }

    [Fact]
    public void ToLocale_EmptyString_ReturnsNl()
    {
        // Arrange.
        var input = string.Empty;

        // Act.
        var result = input.ToLocale();

        // Assert.
        Assert.Equal(Locale.Nl, result);
    }

    [Fact]
    public void ToLocale_UnrecognisedCode_ReturnsNl()
    {
        // Arrange.
        var input = "fr";

        // Act.
        var result = input.ToLocale();

        // Assert.
        Assert.Equal(Locale.Nl, result);
    }

    [Fact]
    public void ToLocale_NlCode_ReturnsNl()
    {
        // Arrange.
        var input = "nl";

        // Act.
        var result = input.ToLocale();

        // Assert.
        Assert.Equal(Locale.Nl, result);
    }

    [Fact]
    public void ToLocale_EnCode_ReturnsEn()
    {
        // Arrange.
        var input = "en";

        // Act.
        var result = input.ToLocale();

        // Assert.
        Assert.Equal(Locale.En, result);
    }

    [Fact]
    public void ToCultureCode_Nl_ReturnsNlString()
    {
        // Arrange.
        var locale = Locale.Nl;

        // Act.
        var result = locale.ToCultureCode();

        // Assert.
        Assert.Equal("nl", result);
    }

    [Fact]
    public void ToCultureCode_En_ReturnsEnString()
    {
        // Arrange.
        var locale = Locale.En;

        // Act.
        var result = locale.ToCultureCode();

        // Assert.
        Assert.Equal("en", result);
    }
}
