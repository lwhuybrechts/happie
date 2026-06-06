using System.Net;
using System.Text.Json;
using Happie.Shared.Contracts;
using Happie.Shared.Domain;
using Happie.Web.Resources;
using Happie.Web.Services;
using Happie.Web.Services.Caching;
using Happie.Web.Tests.Helpers;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using Moq;

namespace Happie.Web.Tests.Services.Caching;

public class SyncServiceTests
{
    private readonly Mock<IMutationQueue> _mutationQueueMock = new();
    private readonly Mock<ICacheStore> _cacheStoreMock = new();
    private readonly Mock<IConnectivityService> _connectivityServiceMock = new();
    private readonly Mock<IJSRuntime> _jsRuntimeMock = new();
    private readonly Mock<IStringLocalizer<AppStrings>> _localizerMock = new();
    private readonly FakeDelayService _fakeDelayService = new();
    private readonly LoadingIndicatorState _loadingIndicatorState;
    private readonly SyncToastState _syncToastState;
    private readonly HttpClient _httpClient;
    private readonly SyncService _sut;

    private const string HouseholdId = "test-household-id";

    private Action<bool>? _connectivityChangedHandler;

    public SyncServiceTests()
    {
        _loadingIndicatorState = new LoadingIndicatorState(_fakeDelayService);
        _syncToastState = new SyncToastState(_fakeDelayService);

        _httpClient = new HttpClient(new FakeHttpMessageHandler(HttpStatusCode.OK))
        {
            BaseAddress = new Uri("http://localhost/api/")
        };

        _localizerMock
            .Setup(x => x[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key));
        _localizerMock
            .Setup(x => x[It.IsAny<string>(), It.IsAny<object[]>()])
            .Returns((string key, object[] arguments) => new LocalizedString(key, key));

        SetupLocalStorageGetItem("householdId", HouseholdId);

        // Capture the connectivity changed event handler so we can raise it in tests.
        _connectivityServiceMock
            .SetupAdd(x => x.OnConnectivityChanged += It.IsAny<Action<bool>>())
            .Callback<Action<bool>>(handler => _connectivityChangedHandler = handler);

        _sut = new SyncService(
            _mutationQueueMock.Object,
            _cacheStoreMock.Object,
            _connectivityServiceMock.Object,
            _loadingIndicatorState,
            _syncToastState,
            _httpClient,
            _fakeDelayService,
            _localizerMock.Object,
            _jsRuntimeMock.Object);
    }

    [Fact]
    public async Task ReplayMutationAsync_SuccessfulReplay_RemovesMutationFromQueue()
    {
        // Arrange.
        var mutation = CreateMutation();
        _httpClient.Dispose();
        var httpClient = new HttpClient(new FakeHttpMessageHandler(HttpStatusCode.OK))
        {
            BaseAddress = new Uri("http://localhost/api/")
        };

        var sut = new SyncService(
            _mutationQueueMock.Object,
            _cacheStoreMock.Object,
            _connectivityServiceMock.Object,
            _loadingIndicatorState,
            _syncToastState,
            httpClient,
            _fakeDelayService,
            _localizerMock.Object,
            _jsRuntimeMock.Object);

        var dequeueCallCount = 0;
        _mutationQueueMock
            .Setup(x => x.DequeueAsync(HouseholdId))
            .ReturnsAsync(() =>
            {
                dequeueCallCount++;
                return dequeueCallCount == 1 ? mutation : null;
            });

        await sut.InitializeAsync();

        // Act.
        _connectivityChangedHandler?.Invoke(true);
        await _fakeDelayService.TriggerTimerAsync();

        // Assert.
        _mutationQueueMock.Verify(x => x.DequeueAsync(HouseholdId), Times.Exactly(2));
    }

    [Fact]
    public async Task ReplayMutationAsync_4xxClientError_RollsBackCacheAndShowsToast()
    {
        // Arrange.
        var housemateId = Guid.NewGuid();
        var date = new DateOnly(2025, 1, 15);
        var mutation = CreateAttendanceMutation(date, housemateId);

        var httpClient = new HttpClient(new FakeHttpMessageHandler(HttpStatusCode.UnprocessableEntity))
        {
            BaseAddress = new Uri("http://localhost/api/")
        };

        var sut = new SyncService(
            _mutationQueueMock.Object,
            _cacheStoreMock.Object,
            _connectivityServiceMock.Object,
            _loadingIndicatorState,
            _syncToastState,
            httpClient,
            _fakeDelayService,
            _localizerMock.Object,
            _jsRuntimeMock.Object);

        SetupDequeueReturnsOnce(mutation);
        SetupCacheWithAttendance(date, housemateId, AttendanceStatus.EatingIn);

        await sut.InitializeAsync();

        // Act.
        _connectivityChangedHandler?.Invoke(true);
        await _fakeDelayService.TriggerTimerAsync();

        // Assert.
        _cacheStoreMock.Verify(x => x.PutDayPlanAsync(HouseholdId, "2025-01-15", It.IsAny<string>()), Times.Once);
        Assert.Single(_syncToastState.VisibleToasts);
    }

    [Fact]
    public async Task ReplayMutationAsync_409Conflict_ShowsConflictToastWithSpecificMessage()
    {
        // Arrange.
        var housemateId = Guid.NewGuid();
        var date = new DateOnly(2025, 1, 15);
        var mutation = CreateAttendanceMutation(date, housemateId);

        var httpClient = new HttpClient(new FakeHttpMessageHandler(HttpStatusCode.Conflict))
        {
            BaseAddress = new Uri("http://localhost/api/")
        };

        var sut = new SyncService(
            _mutationQueueMock.Object,
            _cacheStoreMock.Object,
            _connectivityServiceMock.Object,
            _loadingIndicatorState,
            _syncToastState,
            httpClient,
            _fakeDelayService,
            _localizerMock.Object,
            _jsRuntimeMock.Object);

        SetupDequeueReturnsOnce(mutation);
        SetupCacheWithAttendance(date, housemateId, AttendanceStatus.EatingIn);

        await sut.InitializeAsync();

        // Act.
        _connectivityChangedHandler?.Invoke(true);
        await _fakeDelayService.TriggerTimerAsync();

        // Assert.
        Assert.Single(_syncToastState.VisibleToasts);
        Assert.Equal("Sync_ConflictMessage", _syncToastState.VisibleToasts[0].Message);
    }

    [Fact]
    public async Task ReplayMutationAsync_ExhaustedRetries_RollsBackAndShowsToast()
    {
        // Arrange.
        var housemateId = Guid.NewGuid();
        var date = new DateOnly(2025, 1, 15);
        var mutation = CreateAttendanceMutation(date, housemateId);

        var httpClient = new HttpClient(new FakeHttpMessageHandler(HttpStatusCode.InternalServerError))
        {
            BaseAddress = new Uri("http://localhost/api/")
        };

        var sut = new SyncService(
            _mutationQueueMock.Object,
            _cacheStoreMock.Object,
            _connectivityServiceMock.Object,
            _loadingIndicatorState,
            _syncToastState,
            httpClient,
            _fakeDelayService,
            _localizerMock.Object,
            _jsRuntimeMock.Object);

        SetupDequeueReturnsOnce(mutation);
        SetupCacheWithAttendance(date, housemateId, AttendanceStatus.EatingIn);

        await sut.InitializeAsync();

        // Act.
        _connectivityChangedHandler?.Invoke(true);
        await _fakeDelayService.TriggerTimerAsync();

        // Assert.
        _cacheStoreMock.Verify(x => x.PutDayPlanAsync(HouseholdId, "2025-01-15", It.IsAny<string>()), Times.Once);
        Assert.Single(_syncToastState.VisibleToasts);
        Assert.Equal("Sync_FailureMessage", _syncToastState.VisibleToasts[0].Message);
    }

    private void SetupLocalStorageGetItem(string key, string? value)
    {
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>(
                "localStorage.getItem",
                It.Is<object[]>(x => x.Length == 1 && x[0].ToString() == key)))
            .ReturnsAsync(value);
    }

    private void SetupDequeueReturnsOnce(QueuedMutation mutation)
    {
        var dequeueCallCount = 0;
        _mutationQueueMock
            .Setup(x => x.DequeueAsync(HouseholdId))
            .ReturnsAsync(() =>
            {
                dequeueCallCount++;
                return dequeueCallCount == 1 ? mutation : null;
            });
    }

    private void SetupCacheWithAttendance(DateOnly date, Guid housemateId, AttendanceStatus status)
    {
        var dayPlan = new DayPlanResponse(
            date,
            null,
            new List<AttendanceDto>
            {
                new(housemateId, "Alice", "#FF0000", status, false)
            },
            new List<CommentDto>(),
            new List<HistoryEntryDto>());

        var json = JsonSerializer.Serialize(dayPlan);
        var cachedDayPlan = new CachedDayPlan(date.ToString("yyyy-MM-dd"), json, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        _cacheStoreMock
            .Setup(x => x.GetDayPlanAsync(HouseholdId, date.ToString("yyyy-MM-dd")))
            .ReturnsAsync(cachedDayPlan);
    }

    private static QueuedMutation CreateMutation()
    {
        return new QueuedMutation(
            1,
            HouseholdId,
            "PUT",
            "days/2025-01-15/attendance/" + Guid.NewGuid(),
            new Dictionary<string, string> { { "Authorization", "Bearer test-token" } },
            """{"status":"EatingIn"}""",
            DateTimeOffset.UtcNow,
            new DateOnly(2025, 1, 15),
            "attendance");
    }

    private static QueuedMutation CreateAttendanceMutation(DateOnly date, Guid housemateId)
    {
        return new QueuedMutation(
            1,
            HouseholdId,
            "PUT",
            $"days/{date:yyyy-MM-dd}/attendance/{housemateId}",
            new Dictionary<string, string> { { "Authorization", "Bearer test-token" } },
            """{"status":"EatingIn"}""",
            DateTimeOffset.UtcNow,
            date,
            "attendance");
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;

        public FakeHttpMessageHandler(HttpStatusCode statusCode)
        {
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(_statusCode));
        }
    }
}
