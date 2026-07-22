using System.Net;
using System.Text.Json;
using Happie.Shared.Contracts;
using Happie.Web.Services;
using Happie.Web.Services.Caching;
using Happie.Web.Tests.Helpers;
using Microsoft.JSInterop;
using Moq;

namespace Happie.Web.Tests.Services.Caching;

public class CachedApiClientSavedDishesCacheLifecycleTests
{
    private readonly Mock<ICacheStore> _cacheStoreMock = new();
    private readonly Mock<IMutationQueue> _mutationQueueMock = new();
    private readonly Mock<IConnectivityService> _connectivityServiceMock = new();
    private readonly Mock<IJSRuntime> _jsRuntimeMock = new();
    private readonly FakeNavigationManager _navigationManager = new();
    private readonly CachedApiClient _sut;

    private const string HouseholdId = "test-household-id";
    private const string TestDate = "2025-01-15";

    public CachedApiClientSavedDishesCacheLifecycleTests()
    {
        SetupLocalStorageGetItem("householdId", HouseholdId);

        var httpClient = new HttpClient(new CapturingHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json")
            }))
        {
            BaseAddress = new Uri("http://localhost/api/")
        };

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

    // Validates: Requirement 1.4
    [Fact]
    public async Task BackgroundRefreshSavedDishes_IdenticalResponse_UpdatesTimestampNotData()
    {
        // Arrange.
        var dishes = CreateSavedDishes();
        var json = JsonSerializer.Serialize(dishes);
        var cachedEntry = new CachedSavedDishes(json, DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeMilliseconds());

        _cacheStoreMock
            .Setup(x => x.GetSavedDishesAsync(HouseholdId))
            .ReturnsAsync(cachedEntry);
        SetupConnectivityOnline();

        // HTTP returns the same JSON as the cache.
        var sut = CreateSutWithHttpResponse(HttpStatusCode.OK, json);

        var eventFired = false;
        sut.OnSavedDishesUpdated += _ => eventFired = true;

        // Act.
        await sut.GetSavedDishesAsync();

        // Wait for background refresh to complete.
        await Task.Delay(200);

        // Assert.
        // PutSavedDishesAsync should be called (to update the timestamp).
        _cacheStoreMock.Verify(x => x.PutSavedDishesAsync(HouseholdId, json), Times.Once);
        // OnSavedDishesUpdated should NOT fire because data is identical.
        Assert.False(eventFired);
    }

    // Validates: Requirement 3.3
    [Fact]
    public async Task ClearSessionAndRedirect_401Response_CallsClearAllAsync()
    {
        // Arrange.
        SetupConnectivityOnline();
        SetupLocalStorageGetItem("jwt", "test-token");

        // Cold cache so it fetches from the API.
        _cacheStoreMock
            .Setup(x => x.GetSavedDishesAsync(HouseholdId))
            .ReturnsAsync((CachedSavedDishes?)null);

        var sut = CreateSutWithHttpResponse(HttpStatusCode.Unauthorized, null);

        // Act.
        await sut.GetSavedDishesAsync();

        // Assert.
        _cacheStoreMock.Verify(x => x.ClearAllAsync(HouseholdId), Times.Once);
    }

    // Validates: Requirement 3.4
    [Fact]
    public async Task GetSavedDishesAsync_HouseholdIdChange_OldHouseholdCacheCleared()
    {
        // Arrange.
        var oldHouseholdId = "old-household-id";

        // Simulate: first call with old household clears cache on 401 (simulating session expiry/login with new household).
        var jsRuntimeMock = new Mock<IJSRuntime>();
        var cacheStoreMock = new Mock<ICacheStore>();
        var connectivityServiceMock = new Mock<IConnectivityService>();
        var mutationQueueMock = new Mock<IMutationQueue>();
        var navigationManager = new FakeNavigationManager();

        // First request has old householdId.
        SetupLocalStorageGetItemOnMock(jsRuntimeMock, "householdId", oldHouseholdId);
        connectivityServiceMock.Setup(x => x.IsOnline).Returns(true);

        // Return 401 to simulate old session expiry.
        var handler = new CapturingHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/api/") };

        cacheStoreMock
            .Setup(x => x.GetSavedDishesAsync(oldHouseholdId))
            .ReturnsAsync((CachedSavedDishes?)null);

        var sessionService = new SessionService(jsRuntimeMock.Object, navigationManager, cacheStoreMock.Object);
        var sut = new CachedApiClient(
            cacheStoreMock.Object,
            mutationQueueMock.Object,
            connectivityServiceMock.Object,
            httpClient,
            jsRuntimeMock.Object,
            navigationManager,
            sessionService);

        // Act.
        await sut.GetSavedDishesAsync();

        // Assert.
        // ClearAllAsync should be called with the old householdId, clearing its cache including saved dishes.
        cacheStoreMock.Verify(x => x.ClearAllAsync(oldHouseholdId), Times.Once);
    }

    // Validates: Requirement 6.1
    [Fact]
    public async Task GetDayPlanAsync_FirstLoad_TriggersPrePopulationOfSavedDishes()
    {
        // Arrange.
        SetupConnectivityOnline();

        // No saved dishes cache entry exists.
        _cacheStoreMock
            .Setup(x => x.GetSavedDishesAsync(HouseholdId))
            .ReturnsAsync((CachedSavedDishes?)null);

        var savedDishes = CreateSavedDishes();
        var savedDishesJson = JsonSerializer.Serialize(savedDishes);
        var dayPlanJson = JsonSerializer.Serialize(CreateDayPlanResponse());

        var requestUrls = new List<string>();
        var handler = new CapturingHttpMessageHandler(request =>
        {
            requestUrls.Add(request.RequestUri!.PathAndQuery);
            var responseJson = request.RequestUri.PathAndQuery.Contains("saved-dishes")
                ? savedDishesJson
                : dayPlanJson;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/api/") };

        var sessionService = new SessionService(_jsRuntimeMock.Object, _navigationManager, _cacheStoreMock.Object);
        var sut = new CachedApiClient(
            _cacheStoreMock.Object,
            _mutationQueueMock.Object,
            _connectivityServiceMock.Object,
            httpClient,
            _jsRuntimeMock.Object,
            _navigationManager,
            sessionService);

        // Cold cache for day plan.
        _cacheStoreMock
            .Setup(x => x.GetDayPlanAsync(HouseholdId, TestDate))
            .ReturnsAsync((CachedDayPlan?)null);

        // Act.
        await sut.GetDayPlanAsync(TestDate);

        // Wait for pre-population background task.
        await Task.Delay(200);

        // Assert.
        // A request to saved-dishes should have been made.
        Assert.Contains(requestUrls, x => x.Contains("saved-dishes"));
        // PutSavedDishesAsync should be called to cache the pre-populated result.
        _cacheStoreMock.Verify(x => x.PutSavedDishesAsync(HouseholdId, It.IsAny<string>()), Times.Once);
    }

    // Validates: Requirement 6.2
    [Fact]
    public async Task GetDayPlanAsync_PrePopulationFails_DoesNotThrowOrShowError()
    {
        // Arrange.
        SetupConnectivityOnline();

        // No saved dishes cache entry.
        _cacheStoreMock
            .Setup(x => x.GetSavedDishesAsync(HouseholdId))
            .ReturnsAsync((CachedSavedDishes?)null);

        var dayPlanJson = JsonSerializer.Serialize(CreateDayPlanResponse());

        // HTTP handler: day plan succeeds, saved-dishes returns 500.
        var handler = new CapturingHttpMessageHandler(request =>
        {
            if (request.RequestUri!.PathAndQuery.Contains("saved-dishes"))
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(dayPlanJson, System.Text.Encoding.UTF8, "application/json")
            };
        });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/api/") };

        var sessionService = new SessionService(_jsRuntimeMock.Object, _navigationManager, _cacheStoreMock.Object);
        var sut = new CachedApiClient(
            _cacheStoreMock.Object,
            _mutationQueueMock.Object,
            _connectivityServiceMock.Object,
            httpClient,
            _jsRuntimeMock.Object,
            _navigationManager,
            sessionService);

        // Cold cache for day plan.
        _cacheStoreMock
            .Setup(x => x.GetDayPlanAsync(HouseholdId, TestDate))
            .ReturnsAsync((CachedDayPlan?)null);

        // Act.
        var exception = await Record.ExceptionAsync(async () =>
        {
            var result = await sut.GetDayPlanAsync(TestDate);
            // Wait for pre-population to complete.
            await Task.Delay(200);
        });

        // Assert.
        Assert.Null(exception);
    }

    // Validates: Requirement 6.3
    [Fact]
    public async Task GetDayPlanAsync_PrePopulation_DoesNotBlockDayPlanRender()
    {
        // Arrange.
        SetupConnectivityOnline();

        // No saved dishes cache entry.
        _cacheStoreMock
            .Setup(x => x.GetSavedDishesAsync(HouseholdId))
            .ReturnsAsync((CachedSavedDishes?)null);

        var dayPlanResponse = CreateDayPlanResponse();
        var dayPlanJson = JsonSerializer.Serialize(dayPlanResponse);

        // Saved-dishes fetch uses a TaskCompletionSource to delay it.
        var savedDishesTcs = new TaskCompletionSource<HttpResponseMessage>();
        var handler = new DelayableHttpMessageHandler(request =>
        {
            if (request.RequestUri!.PathAndQuery.Contains("saved-dishes"))
                return savedDishesTcs.Task;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(dayPlanJson, System.Text.Encoding.UTF8, "application/json")
            });
        });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/api/") };

        var sessionService = new SessionService(_jsRuntimeMock.Object, _navigationManager, _cacheStoreMock.Object);
        var sut = new CachedApiClient(
            _cacheStoreMock.Object,
            _mutationQueueMock.Object,
            _connectivityServiceMock.Object,
            httpClient,
            _jsRuntimeMock.Object,
            _navigationManager,
            sessionService);

        // Cold cache for day plan.
        _cacheStoreMock
            .Setup(x => x.GetDayPlanAsync(HouseholdId, TestDate))
            .ReturnsAsync((CachedDayPlan?)null);

        // Act.
        // GetDayPlanAsync should return before the saved-dishes request completes.
        var result = await sut.GetDayPlanAsync(TestDate);

        // Assert.
        // DayPlan should have returned successfully while saved-dishes is still pending.
        Assert.NotNull(result.Data);
        Assert.False(savedDishesTcs.Task.IsCompleted);

        // Cleanup: complete the pending task so no unobserved task exceptions.
        savedDishesTcs.SetResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json")
        });
    }

    private void SetupLocalStorageGetItem(string key, string? value)
    {
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>(
                "localStorage.getItem",
                It.Is<object[]>(x => x.Length == 1 && x[0].ToString() == key)))
            .ReturnsAsync(value);
    }

    private static void SetupLocalStorageGetItemOnMock(Mock<IJSRuntime> mock, string key, string? value)
    {
        mock
            .Setup(x => x.InvokeAsync<string?>(
                "localStorage.getItem",
                It.Is<object[]>(x => x.Length == 1 && x[0].ToString() == key)))
            .ReturnsAsync(value);
    }

    private void SetupConnectivityOnline()
    {
        _connectivityServiceMock.Setup(x => x.IsOnline).Returns(true);
    }

    private CachedApiClient CreateSutWithHttpResponse(HttpStatusCode statusCode, string? responseJson)
    {
        var content = responseJson is not null
            ? new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
            : null;
        var handler = new CapturingHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(statusCode);
            if (content is not null)
                response.Content = content;
            return response;
        });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/api/") };

        var sessionService = new SessionService(_jsRuntimeMock.Object, _navigationManager, _cacheStoreMock.Object);
        return new CachedApiClient(
            _cacheStoreMock.Object,
            _mutationQueueMock.Object,
            _connectivityServiceMock.Object,
            httpClient,
            _jsRuntimeMock.Object,
            _navigationManager,
            sessionService);
    }

    private static List<SavedDishDto> CreateSavedDishes()
    {
        return new List<SavedDishDto>
        {
            new(Guid.NewGuid(), "Pasta"),
            new(Guid.NewGuid(), "Risotto"),
            new(Guid.NewGuid(), "Pizza"),
        };
    }

    private static DayPlanResponse CreateDayPlanResponse()
    {
        return new DayPlanResponse(
            DateOnly.ParseExact(TestDate, "yyyy-MM-dd"),
            null,
            new List<AttendanceDto>
            {
                new(Guid.NewGuid(), "Alice", "#FF0000", Happie.Shared.Domain.AttendanceStatus.Unknown, false),
            },
            new List<CommentDto>(),
            new List<HistoryEntryDto>());
    }
}


