using Bunit;
using Bunit.TestDoubles;
using Happie.Web.Components;
using Happie.Web.Resources;
using Happie.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using Moq;

namespace Happie.Web.Tests.Components;

public class OfflineBannerTests : BunitContext
{
    private readonly Mock<IConnectivityService> _connectivityServiceMock = new();
    private readonly Mock<IStringLocalizer<AppStrings>> _localizerMock = new();

    public OfflineBannerTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        _localizerMock
            .Setup(x => x[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key));

        Services.AddSingleton(_connectivityServiceMock.Object);
        Services.AddSingleton(_localizerMock.Object);
    }

    [Fact]
    public void Render_WhenOfflineOnDayPlanPage_ShowsBanner()
    {
        // Arrange.
        SetupConnectivityOffline();
        SetupNavigationUri("http://localhost/day/2025-01-15");

        // Act.
        var cut = Render<OfflineBanner>();

        // Assert.
        var banner = cut.Find("div.offline-banner[role='alert']");
        Assert.NotNull(banner);
    }

    [Fact]
    public void Render_WhenOfflineOnLoginPage_DoesNotShowBanner()
    {
        // Arrange.
        SetupConnectivityOffline();
        SetupNavigationUri("http://localhost/");

        // Act.
        var cut = Render<OfflineBanner>();

        // Assert.
        Assert.Empty(cut.FindAll("div.offline-banner"));
    }

    [Fact]
    public void Render_WhenOnline_DoesNotShowBanner()
    {
        // Arrange.
        SetupConnectivityOnline();
        SetupNavigationUri("http://localhost/day/2025-01-15");

        // Act.
        var cut = Render<OfflineBanner>();

        // Assert.
        Assert.Empty(cut.FindAll("div.offline-banner"));
    }

    [Fact]
    public void ConnectivityChanged_GoesOfflineThenOnline_TogglesVisibility()
    {
        // Arrange.
        SetupConnectivityOnline();
        SetupNavigationUri("http://localhost/day/2025-01-15");
        var cut = Render<OfflineBanner>();

        // Act — go offline.
        _connectivityServiceMock.Raise(x => x.OnConnectivityChanged += null, false);

        // Assert — banner appears.
        Assert.NotEmpty(cut.FindAll("div.offline-banner"));

        // Act — go back online.
        _connectivityServiceMock.Raise(x => x.OnConnectivityChanged += null, true);

        // Assert — banner hidden.
        Assert.Empty(cut.FindAll("div.offline-banner"));
    }

    private void SetupConnectivityOffline()
    {
        _connectivityServiceMock.Setup(x => x.IsOnline).Returns(false);
    }

    private void SetupConnectivityOnline()
    {
        _connectivityServiceMock.Setup(x => x.IsOnline).Returns(true);
    }

    private void SetupNavigationUri(string uri)
    {
        var navigationManager = Services.GetRequiredService<NavigationManager>();
        var bunitNavigationManager = (BunitNavigationManager)navigationManager;
        bunitNavigationManager.NavigateTo(uri);
    }
}
