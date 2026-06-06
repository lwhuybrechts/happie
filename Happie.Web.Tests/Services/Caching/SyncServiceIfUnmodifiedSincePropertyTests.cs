using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Web.Resources;
using Happie.Web.Services;
using Happie.Web.Services.Caching;
using Happie.Web.Tests.Helpers;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using Moq;

namespace Happie.Web.Tests.Services.Caching;

// Feature: offline-cache, Property 13: Replayed mutations include If-Unmodified-Since header
public class SyncServiceIfUnmodifiedSincePropertyTests
{
    private static readonly Arbitrary<DateTimeOffset> CreatedAtArb =
        Gen.Choose(0, (int)(DateTimeOffset.MaxValue.ToUnixTimeSeconds() / 1000))
            .Select(x => DateTimeOffset.FromUnixTimeSeconds(x * 1000))
            .ToArbitrary();

    // Feature: offline-cache, Property 13: Replayed mutations include If-Unmodified-Since header
    // Validates: Requirements 6.10
    [Property(MaxTest = 100)]
    public Property ReplayedMutation_ContainsIfUnmodifiedSinceHeader_MatchingCreatedAt()
    {
        return Prop.ForAll(
            CreatedAtArb,
            x =>
            {
                var capturedRequest = ReplayMutationAndCaptureRequest(x);

                if (capturedRequest is null)
                    return false.Label("No HTTP request was captured during replay");

                var headerValue = capturedRequest.Headers.IfUnmodifiedSince;

                if (headerValue is null)
                    return false.Label("If-Unmodified-Since header was not present on the request");

                // HTTP date format has per-second precision, so compare at second level.
                var expectedTruncated = new DateTimeOffset(
                    x.Year, x.Month, x.Day,
                    x.Hour, x.Minute, x.Second,
                    x.Offset);

                var actualTruncated = new DateTimeOffset(
                    headerValue.Value.Year, headerValue.Value.Month, headerValue.Value.Day,
                    headerValue.Value.Hour, headerValue.Value.Minute, headerValue.Value.Second,
                    headerValue.Value.Offset);

                return (expectedTruncated == actualTruncated)
                    .Label($"Expected If-Unmodified-Since={expectedTruncated:R}, got {actualTruncated:R}");
            });
    }

    private static HttpRequestMessage? ReplayMutationAndCaptureRequest(DateTimeOffset createdAt)
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new CapturingHttpMessageHandler(request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/api/") };

        var mutationQueueMock = new Mock<IMutationQueue>();
        var dequeueCallCount = 0;
        var mutation = CreateMutation(createdAt);

        mutationQueueMock
            .Setup(x => x.DequeueAsync(It.IsAny<string>()))
            .ReturnsAsync(() =>
            {
                dequeueCallCount++;
                return dequeueCallCount == 1 ? mutation : null;
            });

        var cacheStoreMock = new Mock<ICacheStore>();
        var connectivityServiceMock = new Mock<IConnectivityService>();
        var fakeDelayService = new FakeDelayService();
        var loadingIndicatorState = new LoadingIndicatorState(fakeDelayService);
        var syncToastState = new SyncToastState(fakeDelayService);

        var localizerMock = new Mock<IStringLocalizer<AppStrings>>();
        localizerMock
            .Setup(x => x[It.IsAny<string>(), It.IsAny<object[]>()])
            .Returns((string key, object[] _) => new LocalizedString(key, key));

        var jsRuntimeMock = new Mock<IJSRuntime>();
        jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>("localStorage.getItem", It.IsAny<object[]>()))
            .ReturnsAsync("test-household-id");

        var sut = new SyncService(
            mutationQueueMock.Object,
            cacheStoreMock.Object,
            connectivityServiceMock.Object,
            loadingIndicatorState,
            syncToastState,
            httpClient,
            fakeDelayService,
            localizerMock.Object,
            jsRuntimeMock.Object);

        // Initialize to subscribe to connectivity events.
        sut.InitializeAsync().GetAwaiter().GetResult();

        // Simulate going online.
        connectivityServiceMock.Raise(x => x.OnConnectivityChanged += null, true);

        // Trigger the replay timer.
        fakeDelayService.TriggerTimerAsync().GetAwaiter().GetResult();

        sut.Dispose();
        return capturedRequest;
    }

    private static QueuedMutation CreateMutation(DateTimeOffset createdAt)
    {
        return new QueuedMutation(
            Id: 1,
            HouseholdId: "test-household-id",
            Method: "PUT",
            Url: "http://localhost/api/days/2024-01-15/attendance/00000000-0000-0000-0000-000000000001",
            Headers: new Dictionary<string, string>
            {
                ["Authorization"] = "Bearer test-token",
                ["X-Housemate-Id"] = "00000000-0000-0000-0000-000000000001"
            },
            Body: "{\"status\":\"EatingIn\"}",
            CreatedAt: createdAt,
            Date: new DateOnly(2024, 1, 15),
            MutationType: "attendance");
    }
}


