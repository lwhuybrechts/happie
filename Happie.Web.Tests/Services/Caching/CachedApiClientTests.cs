using System.Net;
using System.Text.Json;
using Happie.Shared.Contracts;
using Happie.Shared.Domain;
using Happie.Web.Services;
using Happie.Web.Services.Caching;
using Happie.Web.Tests.Helpers;
using Microsoft.JSInterop;
using Moq;
using RichardSzalay.MockHttp;

namespace Happie.Web.Tests.Services.Caching;

public class CachedApiClientTests
{
    private readonly Mock<ICacheStore> _cacheStoreMock = new();
    private readonly Mock<IMutationQueue> _mutationQueueMock = new();
    private readonly Mock<IConnectivityService> _connectivityServiceMock = new();
    private readonly Mock<IJSRuntime> _jsRuntimeMock = new();
    private readonly RichardSzalay.MockHttp.MockHttpMessageHandler _mockHttp = new();
    private readonly FakeNavigationManager _navigationManager = new();
    private readonly CachedApiClient _sut;

    private const string HouseholdId = "test-household-id";
    private const string TestDate = "2025-01-15";

    public CachedApiClientTests()
    {
        var httpClient = _mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("http://localhost/api/");

        SetupLocalStorageGetItem("householdId", HouseholdId);

        var sessionService = new SessionService(_jsRuntimeMock.Object, _navigationManager, _cacheStoreMock.Object);

        _sut = new CachedApiClient(
            _cacheStoreMock.Object,
            _mutationQueueMock.Object,
            _connectivityServiceMock.Object,
            httpClient,
            _jsRuntimeMock.Object,
            _navigationManager,
            sessionService);
    }

    [Fact]
    public async Task GetDayPlanAsync_ColdCache_FetchesFromApiAndStoresInCache()
    {
        // Arrange.
        var dayPlanResponse = CreateDayPlanResponse();
        var json = JsonSerializer.Serialize(dayPlanResponse);

        SetupCacheReturnsNull();
        SetupConnectivityOnline();
        _mockHttp.When($"http://localhost/api/days/{TestDate}")
            .Respond("application/json", json);

        // Act.
        var result = await _sut.GetDayPlanAsync(TestDate);

        // Assert.
        Assert.True(result.IsColdCacheFetch);
        Assert.NotNull(result);
        _cacheStoreMock.Verify(x => x.PutDayPlanAsync(HouseholdId, TestDate, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task GetDayPlanAsync_StaleWhileRevalidate_ReturnsCachedAndTriggersBackgroundRefresh()
    {
        // Arrange.
        var dayPlanResponse = CreateDayPlanResponse();
        var cachedJson = JsonSerializer.Serialize(dayPlanResponse);
        var cachedEntry = new CachedDayPlan(TestDate, cachedJson, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        _cacheStoreMock
            .Setup(x => x.GetDayPlanAsync(HouseholdId, TestDate))
            .ReturnsAsync(cachedEntry);
        SetupConnectivityOnline();
        _mockHttp.When($"http://localhost/api/days/{TestDate}")
            .Respond("application/json", cachedJson);

        // Act.
        var result = await _sut.GetDayPlanAsync(TestDate);

        // Assert.
        Assert.False(result.IsColdCacheFetch);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task SaveAttendanceAsync_Offline_EnqueuesMutationAndAppliesOptimisticUpdate()
    {
        // Arrange.
        var housemateId = Guid.NewGuid();
        var dayPlanResponse = CreateDayPlanResponse(housemateId);
        var cachedJson = JsonSerializer.Serialize(dayPlanResponse);
        var cachedEntry = new CachedDayPlan(TestDate, cachedJson, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        SetupConnectivityOffline();
        SetupLocalStorageGetItem("jwt", "test-token");
        SetupLocalStorageGetItem("activeHousemateId", housemateId.ToString());
        _cacheStoreMock
            .Setup(x => x.GetDayPlanAsync(HouseholdId, TestDate))
            .ReturnsAsync(cachedEntry);

        // Act.
        var result = await _sut.SaveAttendanceAsync(TestDate, housemateId, AttendanceStatus.EatingIn);

        // Assert.
        Assert.True(result);
        _mutationQueueMock.Verify(
            x => x.EnqueueAsync(HouseholdId, It.Is<QueuedMutation>(x => x.Method == "PUT")),
            Times.Once);
        _cacheStoreMock.Verify(x => x.PutDayPlanAsync(HouseholdId, TestDate, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task SaveAttendanceAsync_Offline_DoesNotCallHttpClient()
    {
        // Arrange.
        var housemateId = Guid.NewGuid();

        SetupConnectivityOffline();
        SetupLocalStorageGetItem("jwt", "test-token");
        SetupLocalStorageGetItem("activeHousemateId", housemateId.ToString());
        _cacheStoreMock
            .Setup(x => x.GetDayPlanAsync(HouseholdId, TestDate))
            .ReturnsAsync((CachedDayPlan?)null);

        // Act.
        await _sut.SaveAttendanceAsync(TestDate, housemateId, AttendanceStatus.EatingIn);

        // Assert.
        _mockHttp.VerifyNoOutstandingRequest();
    }

    [Fact]
    public async Task GetDayPlanAsync_ColdCache401_ClearsSessionAndRedirects()
    {
        // Arrange.
        SetupCacheReturnsNull();
        SetupConnectivityOnline();
        _mockHttp.When($"http://localhost/api/days/{TestDate}")
            .Respond(HttpStatusCode.Unauthorized);

        // Act.
        var result = await _sut.GetDayPlanAsync(TestDate);

        // Assert.
        Assert.Null(result.Data);
        _cacheStoreMock.Verify(x => x.ClearAllAsync(HouseholdId), Times.Once);
        _jsRuntimeMock.Verify(
            x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                "localStorage.removeItem",
                It.Is<object[]>(x => x.Length == 1 && x[0].ToString() == "jwt")),
            Times.Once);
        Assert.Equal("/", _navigationManager.LastNavigatedUri);
        Assert.True(_navigationManager.LastForceLoad);
    }

    [Fact]
    public async Task GetDayPlanAsync_IndexedDbUnavailable_DoesNotThrow()
    {
        // Arrange.
        _cacheStoreMock
            .Setup(x => x.GetDayPlanAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((CachedDayPlan?)null);
        _mutationQueueMock
            .Setup(x => x.EnqueueAsync(It.IsAny<string>(), It.IsAny<QueuedMutation>()))
            .Returns(Task.CompletedTask);
        SetupConnectivityOnline();

        var dayPlanResponse = CreateDayPlanResponse();
        var json = JsonSerializer.Serialize(dayPlanResponse);
        _mockHttp.When($"http://localhost/api/days/{TestDate}")
            .Respond("application/json", json);

        // Act.
        var exception = await Record.ExceptionAsync(() => _sut.GetDayPlanAsync(TestDate));

        // Assert.
        Assert.Null(exception);
    }

    [Fact]
    public async Task GetDayPlanAsync_NullHouseholdId_ClearsSessionAndRedirectsToLogin()
    {
        // Arrange.
        SetupLocalStorageGetItem("householdId", null!);
        SetupCacheReturnsNull();
        SetupConnectivityOnline();

        // Act.
        var result = await _sut.GetDayPlanAsync(TestDate);

        // Assert.
        Assert.Null(result.Data);
        _jsRuntimeMock.Verify(
            x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                "localStorage.removeItem",
                It.Is<object[]>(x => x.Length == 1 && x[0].ToString() == "jwt")),
            Times.Once);
        _jsRuntimeMock.Verify(
            x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                "localStorage.removeItem",
                It.Is<object[]>(x => x.Length == 1 && x[0].ToString() == "activeHousemateId")),
            Times.Once);
        Assert.Equal("/", _navigationManager.LastNavigatedUri);
        Assert.True(_navigationManager.LastForceLoad);
    }

    private void SetupLocalStorageGetItem(string key, string? value)
    {
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>(
                "localStorage.getItem",
                It.Is<object[]>(x => x.Length == 1 && x[0].ToString() == key)))
            .ReturnsAsync(value);
    }

    private void SetupCacheReturnsNull()
    {
        _cacheStoreMock
            .Setup(x => x.GetDayPlanAsync(HouseholdId, TestDate))
            .ReturnsAsync((CachedDayPlan?)null);
    }

    private void SetupConnectivityOnline()
    {
        _connectivityServiceMock.Setup(x => x.IsOnline).Returns(true);
    }

    private void SetupConnectivityOffline()
    {
        _connectivityServiceMock.Setup(x => x.IsOnline).Returns(false);
    }

    private static DayPlanResponse CreateDayPlanResponse(Guid? housemateId = null)
    {
        var id = housemateId ?? Guid.NewGuid();
        return new DayPlanResponse(
            DateOnly.ParseExact(TestDate, "yyyy-MM-dd"),
            null,
            new List<AttendanceDto>
            {
                new(id, "Alice", "#FF0000", AttendanceStatus.Unknown, false),
            },
            new List<CommentDto>(),
            new List<HistoryEntryDto>());
    }
}
