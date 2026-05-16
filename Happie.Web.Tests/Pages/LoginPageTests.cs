using Bunit;
using Happie.Web.Pages;
using Happie.Web.Resources;
using Happie.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using RichardSzalay.MockHttp;
using System.Net;

namespace Happie.Web.Tests.Pages;

public class LoginPageTests : BunitContext
{
    private readonly MockHttpMessageHandler _mockHttp = new();
    private readonly Mock<IStringLocalizer<AppStrings>> _localizerMock = new();

    public LoginPageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        SetupLocalizer();

        var httpClient = _mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("http://localhost/api/");
        Services.AddSingleton(httpClient);

        Services.AddSingleton(_localizerMock.Object);
        Services.AddScoped<LocaleService>();
    }

    [Fact]
    public void Render_PasswordFormState_ShowsLogoHeadingSubtitlePasswordFieldLockIconSubmitButtonAndLocaleToggle()
    {
        // Arrange.
        // No JWT in localStorage — bUnit JSInterop in loose mode returns default (null).

        // Act.
        var cut = Render<LoginPage>();

        // Assert.
        var logo = cut.Find(".login-logo");
        Assert.Equal("H", logo.TextContent);

        var heading = cut.Find("h1");
        Assert.Equal("Login_WelcomeHeading", heading.TextContent);

        var subtitle = cut.Find("p.login-subtitle");
        Assert.Equal("Login_Subtitle", subtitle.TextContent);

        var passwordInput = cut.Find("input[type=password]");
        Assert.NotNull(passwordInput);

        var lockIcon = cut.Find(".password-field svg");
        Assert.NotNull(lockIcon);

        var submitButton = cut.Find("button[type=submit]");
        Assert.Equal("Login_SubmitButton", submitButton.TextContent);

        var localeButtons = cut.FindAll(".locale-btn");
        Assert.Equal(2, localeButtons.Count);
        Assert.Equal("EN", localeButtons[0].TextContent);
        Assert.Equal("NL", localeButtons[1].TextContent);
    }

    [Fact]
    public async Task SubmitLoginAsync_FailedLogin_ShowsErrorAlert()
    {
        // Arrange.
        _mockHttp.When("/api/auth/login")
            .Respond(HttpStatusCode.Unauthorized);

        var cut = Render<LoginPage>();

        var passwordInput = cut.Find("input[type=password]");
        passwordInput.Change("wrong-password");

        // Act.
        var form = cut.Find("form");
        form.Submit();

        // Wait for the async operation to complete.
        cut.WaitForState(() => cut.FindAll("[role=alert]").Count > 0);

        // Assert.
        var alert = cut.Find("[role=alert]");
        Assert.NotNull(alert);
    }

    [Fact]
    public async Task SubmitLoginAsync_WhileSubmitting_SubmitButtonIsDisabled()
    {
        // Arrange.
        // Set up a delayed response so _isSubmitting stays true during the request.
        var tcs = new TaskCompletionSource<HttpResponseMessage>();
        _mockHttp.When("/api/auth/login")
            .Respond(_ => tcs.Task);

        var cut = Render<LoginPage>();

        var passwordInput = cut.Find("input[type=password]");
        passwordInput.Change("test-password");

        // Act.
        var form = cut.Find("form");
        _ = cut.InvokeAsync(() => form.Submit());

        // Assert.
        var submitButton = cut.Find("button[type=submit]");
        Assert.True(submitButton.HasAttribute("disabled"));

        // Clean up — complete the pending request.
        tcs.SetResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
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
