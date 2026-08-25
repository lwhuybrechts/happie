using Bunit;
using Happie.Web.Components;
using Happie.Web.Resources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;

namespace Happie.Web.Tests.Components;

public class LoadingDotsTests : BunitContext
{
    private readonly Mock<IStringLocalizer<AppStrings>> _localizerMock = new();

    public LoadingDotsTests()
    {
        _localizerMock
            .Setup(x => x[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key));

        Services.AddSingleton(_localizerMock.Object);
    }

    [Fact]
    public void Render_ShowsThreeDotElements()
    {
        // Act.
        var cut = Render<LoadingDots>();

        // Assert.
        var dots = cut.FindAll(".loading-dots__dot");
        Assert.Equal(3, dots.Count);
    }

    [Fact]
    public void Render_RootElementHasRoleStatus()
    {
        // Act.
        var cut = Render<LoadingDots>();

        // Assert.
        var root = cut.Find(".loading-dots");
        Assert.Equal("status", root.GetAttribute("role"));
    }

    [Fact]
    public void Render_HasNonEmptyAriaLabel()
    {
        // Act.
        var cut = Render<LoadingDots>();

        // Assert.
        var root = cut.Find(".loading-dots");
        var ariaLabel = root.GetAttribute("aria-label");
        Assert.False(string.IsNullOrEmpty(ariaLabel));
    }
}
