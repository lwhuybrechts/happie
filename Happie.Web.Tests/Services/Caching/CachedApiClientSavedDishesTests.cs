using System.Net;
using System.Text.Json;
using Happie.Shared.Contracts;
using Happie.Web.Services;
using Happie.Web.Services.Caching;
using Happie.Web.Tests.Helpers;
using Microsoft.JSInterop;
using Moq;
using RichardSzalay.MockHttp;

namespace Happie.Web.Tests.Services.Caching;

public class CachedApiClientSavedDishesTests
{
    private readonly Mock<ICacheStore> _cacheStoreMock = new();
    private readonly Mock<IMutationQueue> _mutationQueueMock = new();
    private readonly Mock<IConnectivityService> _connectivityServiceMock = new();
    private readonly Mock<IJSRuntime> _jsRuntimeMock = new();
    private readonly RichardSzalay.MockHttp.MockHttpMessageHandler _mockHttp = new();
    private readonly FakeNavigationManager _navigationManager = new();
    private readonly CachedApiClient _sut;

    private const string HouseholdId = "test-household-id";

    public CachedApiClientSavedDishesTests()
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
    public async Task GetSavedDishesAsync_ColdCacheAndOffline_ReturnsIsColdCacheTrue()
    {
        // Arrange.
        SetupConnectivityOffline();
        SetupSavedDishCacheReturnsNull();

        // Act.
        var result = await _sut.GetSavedDishesAsync();

        // Assert.
        Assert.True(result.IsColdCache);
        Assert.Null(result.Dishes);
        Assert.False(result.HasError);
    }

    [Fact]
    public async Task GetSavedDishesAsync_CachedEntryAndOffline_DoesNotAttemptBackgroundRefresh()
    {
        // Arrange.
        var dishes = CreateSavedDishes();
        var json = JsonSerializer.Serialize(dishes);
        var cachedEntry = new CachedSavedDishes(json, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        SetupConnectivityOffline();
        _cacheStoreMock
            .Setup(x => x.GetSavedDishesAsync(HouseholdId))
            .ReturnsAsync(cachedEntry);

        // Act.
        var result = await _sut.GetSavedDishesAsync();

        // Assert.
        Assert.NotNull(result.Dishes);
        Assert.Equal(2, result.Dishes.Count);
        _mockHttp.VerifyNoOutstandingRequest();
    }

    [Fact]
    public async Task GetSavedDishesAsync_CachedEntryAndOffline_ConnectivityReportsFalseForPromoteDisable()
    {
        // Arrange.
        var dishes = CreateSavedDishes();
        var json = JsonSerializer.Serialize(dishes);
        var cachedEntry = new CachedSavedDishes(json, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        SetupConnectivityOffline();
        _cacheStoreMock
            .Setup(x => x.GetSavedDishesAsync(HouseholdId))
            .ReturnsAsync(cachedEntry);

        // Act.
        var result = await _sut.GetSavedDishesAsync();

        // Assert.
        // Dishes are available from cache (modal can open).
        Assert.NotNull(result.Dishes);
        // But connectivity is offline (promote button should be disabled by the UI).
        Assert.False(_connectivityServiceMock.Object.IsOnline);
    }

    [Fact]
    public async Task GetSavedDishesAsync_BackgroundRefreshReturns401_ClearsSessionAndRedirects()
    {
        // Arrange.
        var dishes = CreateSavedDishes();
        var json = JsonSerializer.Serialize(dishes);
        var cachedEntry = new CachedSavedDishes(json, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        SetupConnectivityOnline();
        _cacheStoreMock
            .Setup(x => x.GetSavedDishesAsync(HouseholdId))
            .ReturnsAsync(cachedEntry);

        _mockHttp.When("http://localhost/api/saved-dishes")
            .Respond(HttpStatusCode.Unauthorized);

        // Act.
        await _sut.GetSavedDishesAsync();

        // Allow background refresh to complete.
        await Task.Delay(200);

        // Assert.
        _cacheStoreMock.Verify(x => x.ClearAllAsync(HouseholdId), Times.Once);
        _jsRuntimeMock.Verify(
            x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                "localStorage.removeItem",
                It.Is<object[]>(x => x.Length == 1 && x[0].ToString() == "jwt")),
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

    private void SetupConnectivityOnline()
    {
        _connectivityServiceMock.Setup(x => x.IsOnline).Returns(true);
    }

    private void SetupConnectivityOffline()
    {
        _connectivityServiceMock.Setup(x => x.IsOnline).Returns(false);
    }

    private void SetupSavedDishCacheReturnsNull()
    {
        _cacheStoreMock
            .Setup(x => x.GetSavedDishesAsync(HouseholdId))
            .ReturnsAsync((CachedSavedDishes?)null);
    }

    private static List<SavedDishDto> CreateSavedDishes()
    {
        return new List<SavedDishDto>
        {
            new(Guid.NewGuid(), "Pasta Bolognese"),
            new(Guid.NewGuid(), "Risotto"),
        };
    }
}
