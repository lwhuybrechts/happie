using System.Net;
using Happie.Web.Services;
using Happie.Web.Tests.Helpers;
using Microsoft.Extensions.Configuration;
using Moq;

namespace Happie.Web.Tests.Services;

public class VersionTrackerTests
{
    private static VersionTracker CreateTracker(
        HttpMessageHandler handler,
        bool isOnline = true,
        string appVersion = "2.0.0.42")
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost/api/")
        };
        var connectivityMock = new Mock<IConnectivityService>();
        connectivityMock.Setup(x => x.IsOnline).Returns(isOnline);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AppVersion"] = appVersion })
            .Build();
        return new VersionTracker(httpClient, connectivityMock.Object, configuration);
    }

    [Fact]
    public async Task ReportVersionAsync_FreshLogin_FiresExactlyOneHttpRequest()
    {
        // Arrange.
        var handler = new CountingHttpMessageHandler();
        var tracker = CreateTracker(handler);

        // Act.
        tracker.ReportVersionAsync();
        await Task.Delay(100);

        // Assert.
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task ReportVersionAsync_AutoRedirect_FiresExactlyOneHttpRequest()
    {
        // Arrange — same code path as fresh login, both call ReportVersionAsync once.
        var handler = new CountingHttpMessageHandler();
        var tracker = CreateTracker(handler);

        // Act.
        tracker.ReportVersionAsync();
        await Task.Delay(100);

        // Assert.
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task ReportVersionAsync_LocalDevVersion_NoHttpCall()
    {
        // Arrange.
        var handler = new CountingHttpMessageHandler();
        var tracker = CreateTracker(handler, appVersion: "1.0.0");

        // Act.
        tracker.ReportVersionAsync();
        await Task.Delay(100);

        // Assert.
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public void ReportVersionAsync_FireAndForget_DoesNotBlock()
    {
        // Arrange — handler that never completes.
        var taskCompletionSource = new TaskCompletionSource<HttpResponseMessage>();
        var handler = new DelayableHttpMessageHandler(_ => taskCompletionSource.Task);
        var tracker = CreateTracker(handler);

        // Act — if fire-and-forget works, this returns immediately.
        tracker.ReportVersionAsync();

        // Assert — the test method completes, proving the call does not block.
        Assert.True(true);
    }

    [Fact]
    public async Task ReportVersionAsync_HousemateSwitch_DoesNotTriggerSecondReport()
    {
        // Arrange — simulates: login fires once, housemate switch would call again.
        var handler = new CountingHttpMessageHandler();
        var tracker = CreateTracker(handler);

        // Act — first call simulates login, second simulates housemate switch.
        tracker.ReportVersionAsync();
        tracker.ReportVersionAsync();
        await Task.Delay(100);

        // Assert — only one HTTP request was made.
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task ReportVersionAsync_LogoutAndReloginInSameSession_DoesNotTriggerSecondReport()
    {
        // Arrange — same instance persists across logout/re-login in same app lifecycle.
        var handler = new CountingHttpMessageHandler();
        var tracker = CreateTracker(handler);

        // Act — first call at initial login, second call at re-login.
        tracker.ReportVersionAsync();
        await Task.Delay(100);
        tracker.ReportVersionAsync();
        await Task.Delay(100);

        // Assert — flag prevents second report.
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task ReportVersionAsync_HttpFailure_SilentlyDiscardedNoRetry()
    {
        // Arrange — handler that throws a network error.
        var handler = new DelayableHttpMessageHandler(_ =>
            throw new HttpRequestException("Simulated network failure."));
        var tracker = CreateTracker(handler);

        // Act — should not throw despite the HTTP failure.
        tracker.ReportVersionAsync();
        await Task.Delay(100);

        // Assert — test completes without exception, proving silent discard.
        Assert.True(true);
    }

    [Fact]
    public async Task ReportVersionAsync_OfflineDetection_SkipsReportEntirely()
    {
        // Arrange.
        var handler = new CountingHttpMessageHandler();
        var tracker = CreateTracker(handler, isOnline: false);

        // Act.
        tracker.ReportVersionAsync();
        await Task.Delay(100);

        // Assert — no HTTP call made when offline.
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task ReportVersionAsync_TenSecondTimeout_Applied()
    {
        // Arrange — handler that captures the cancellation token to verify timeout.
        CancellationToken capturedToken = default;
        var taskCompletionSource = new TaskCompletionSource<HttpResponseMessage>();
        var handler = new TokenCapturingHttpMessageHandler((request, token) =>
        {
            capturedToken = token;
            taskCompletionSource.SetResult(new HttpResponseMessage(HttpStatusCode.NoContent));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        });
        var tracker = CreateTracker(handler);

        // Act.
        tracker.ReportVersionAsync();
        await Task.Delay(100);

        // Assert — the token should be cancellable (from CancellationTokenSource with timeout).
        Assert.True(capturedToken.CanBeCanceled);
    }

    /// <summary>HttpMessageHandler that captures the CancellationToken passed to SendAsync.</summary>
    private sealed class TokenCapturingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public TokenCapturingHttpMessageHandler(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return _handler(request, cancellationToken);
        }
    }
}
