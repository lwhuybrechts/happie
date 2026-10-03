using System.Net;
using Bunit;
using Happie.Web.Pages;
using Happie.Web.Resources;
using Happie.Web.Services;
using Happie.Web.Services.Caching;
using Happie.Web.Tests.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Moq;
using RichardSzalay.MockHttp;
using MockHttp = RichardSzalay.MockHttp.MockHttpMessageHandler;

namespace Happie.Web.Tests.Pages;

public class HousematesPageLoadingTests : BunitContext
{
    private readonly MockHttp _mockHttp = new();
    private readonly Mock<IStringLocalizer<AppStrings>> _localizerMock = new();
    private readonly Mock<IConnectivityService> _connectivityMock = new();

    public HousematesPageLoadingTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        SetupLocalizer();
        _connectivityMock.Setup(x => x.IsOnline).Returns(true);

        var httpClient = _mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("http://localhost/api/");
        Services.AddSingleton(httpClient);

        Services.AddSingleton(_localizerMock.Object);
        Services.AddSingleton(_connectivityMock.Object);
        Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        Services.AddSingleton(new Mock<ICacheStore>().Object);

        Services.AddSingleton(serviceProvider =>
            new LocaleService(serviceProvider.GetRequiredService<IJSRuntime>()));
        Services.AddScoped(serviceProvider =>
            new ActiveHousemateService(serviceProvider.GetRequiredService<IJSRuntime>()));
        Services.AddScoped<SessionService>();
        Services.AddSingleton(serviceProvider =>
            new PushNotificationService(
                serviceProvider.GetRequiredService<IJSRuntime>(),
                serviceProvider.GetRequiredService<HttpClient>(),
                serviceProvider.GetRequiredService<LocaleService>(),
                serviceProvider.GetRequiredService<IConfiguration>(),
                NullLogger<PushNotificationService>.Instance));
        Services.AddSingleton(new SyncToastState(new FakeDelayService()));
    }

    [Fact]
    public void WhileLoading_RendersLoadingDotsInsideLoadingDelayedContainer()
    {
        // Arrange — hold the housemates request pending so the component stays in loading state.
        var taskCompletionSource = new TaskCompletionSource<HttpResponseMessage>();
        _mockHttp
            .When(HttpMethod.Get, "http://localhost/api/housemates")
            .Respond(_ => taskCompletionSource.Task);

        // Act.
        var cut = Render<HousematesPage>();

        // Assert — LoadingDots component is rendered inside a .loading-delayed container.
        var loadingContainer = cut.Find(".loading-delayed");
        Assert.NotNull(loadingContainer);
        Assert.NotEmpty(loadingContainer.QuerySelectorAll(".loading-dots"));

        // Clean up.
        taskCompletionSource.SetResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json"),
        });
    }

    [Fact]
    public void WhileLoading_DoesNotRenderOldPlainTextLoadingMessage()
    {
        // Arrange — hold the housemates request pending so the component stays in loading state.
        var taskCompletionSource = new TaskCompletionSource<HttpResponseMessage>();
        _mockHttp
            .When(HttpMethod.Get, "http://localhost/api/housemates")
            .Respond(_ => taskCompletionSource.Task);

        // Act.
        var cut = Render<HousematesPage>();

        // Assert — no plain <p> element with the old loading text key is rendered.
        var paragraphs = cut.FindAll("p");
        Assert.DoesNotContain(paragraphs, x => x.TextContent.Contains("DayPlan_Loading"));

        // Clean up.
        taskCompletionSource.SetResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json"),
        });
    }

    [Fact]
    public void AfterLoadCompletes_LoadingDotsAreNotRendered()
    {
        // Arrange — return an immediate successful response.
        _mockHttp
            .When(HttpMethod.Get, "http://localhost/api/housemates")
            .Respond("application/json", "[]");

        // Act.
        var cut = Render<HousematesPage>();
        cut.WaitForState(() => cut.FindAll(".loading-dots").Count == 0, TimeSpan.FromSeconds(5));

        // Assert — loading dots are no longer rendered after load completes.
        Assert.Empty(cut.FindAll(".loading-dots"));
        Assert.Empty(cut.FindAll(".loading-delayed"));
    }

    private void SetupLocalizer()
    {
        _localizerMock
            .Setup(x => x[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key));

        _localizerMock
            .Setup(x => x[It.IsAny<string>(), It.IsAny<object[]>()])
            .Returns((string key, object[] _) => new LocalizedString(key, key));
    }
}
