using System.Net;
using System.Text.Json;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Shared.Contracts;
using Happie.Web.Services;
using Happie.Web.Services.Caching;
using Happie.Web.Tests.Helpers;
using Microsoft.JSInterop;
using Moq;

namespace Happie.Web.Tests.Services.Caching;

// Feature: saved-dishes-cache, Property 2: Background refresh replaces cache when data differs
public class CachedApiClientSavedDishesPropertyTests
{
    private static readonly Arbitrary<List<SavedDishDto>> SavedDishListArb =
        Gen.Choose(0, 50)
            .SelectMany(count => Gen.ListOf(
                ArbMap.Default.GeneratorFor<Guid>()
                    .SelectMany(id => Gen.Elements(
                        "Pasta", "Risotto", "Spaghetti", "Pizza", "Salad",
                        "Soup", "Tacos", "Curry", "Stir-fry", "Lasagna",
                        "Noodles", "Burrito", "Sandwich", "Quiche", "Stew")
                        .Select(description => new SavedDishDto(id, description))),
                count))
            .Select(x => x.ToList())
            .ToArbitrary();

    // Feature: saved-dishes-cache, Property 2: Background refresh replaces cache when data differs
    // Validates: Requirements 1.3, 4.2
    [Property(MaxTest = 100)]
    public Property GetSavedDishesAsync_BackgroundRefresh_ReplacesCacheWhenDataDiffers()
    {
        return Prop.ForAll(
            SavedDishListArb,
            SavedDishListArb,
            (initialList, freshList) =>
            {
                var initialJson = JsonSerializer.Serialize(initialList);
                var freshJson = JsonSerializer.Serialize(freshList);

                // Guarantee the lists differ — skip if they happen to be identical.
                if (initialJson == freshJson)
                    return true.Label("Skipped: lists are identical");

                var result = ExecuteBackgroundRefreshAndVerify(initialList, freshList);

                return result.CacheWasUpdatedWithFreshData
                    .Label($"Expected cache to be updated with fresh data. " +
                           $"Initial count: {initialList.Count}, Fresh count: {freshList.Count}, " +
                           $"PutSavedDishes called: {result.PutSavedDishesCalled}, " +
                           $"Stored JSON matches fresh: {result.CacheWasUpdatedWithFreshData}")
                    .And(result.EventWasFiredWithFreshData
                        .Label($"Expected OnSavedDishesUpdated event to fire with fresh list. " +
                               $"Event fired: {result.EventWasFired}, " +
                               $"Event data matches: {result.EventWasFiredWithFreshData}"));
            });
    }

    // Feature: saved-dishes-cache, Property 3: Cold cache fetch stores and returns data
    // Validates: Requirements 1.5, 4.3
    [Property(MaxTest = 100)]
    public Property GetSavedDishesAsync_ColdCache_FetchesStoresAndReturnsList()
    {
        return Prop.ForAll(
            SavedDishListArb,
            async dishes =>
            {
                // Arrange.
                var householdId = Guid.NewGuid().ToString();
                var responseJson = JsonSerializer.Serialize(dishes);

                var cacheStoreMock = new Mock<ICacheStore>();
                var mutationQueueMock = new Mock<IMutationQueue>();
                var connectivityServiceMock = new Mock<IConnectivityService>();
                var jsRuntimeMock = new Mock<IJSRuntime>();
                var navigationManager = new FakeNavigationManager();

                // Online so the cold cache path fetches from the API.
                connectivityServiceMock.Setup(x => x.IsOnline).Returns(true);

                SetupLocalStorageGetItem(jsRuntimeMock, "householdId", householdId);

                // Mock ICacheStore.GetSavedDishesAsync to return null (cold cache).
                cacheStoreMock
                    .Setup(x => x.GetSavedDishesAsync(householdId))
                    .ReturnsAsync((CachedSavedDishes?)null);

                // Mock HTTP handler to return the generated list as JSON.
                var handler = new CapturingHttpMessageHandler(_ =>
                {
                    var response = new HttpResponseMessage(HttpStatusCode.OK);
                    response.Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json");
                    return response;
                });
                var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/api/") };

                // Track PutSavedDishesAsync calls.
                string? storedJson = null;
                cacheStoreMock
                    .Setup(x => x.PutSavedDishesAsync(householdId, It.IsAny<string>()))
                    .Callback<string, string>((_, json) => storedJson = json)
                    .Returns(Task.CompletedTask);

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
                var result = await sut.GetSavedDishesAsync();

                // Assert.
                // Verify the returned dishes match the generated list.
                var dishesMatch = result.Dishes is not null &&
                    result.Dishes.Count == dishes.Count &&
                    result.Dishes.Select(x => (x.Id, x.Description))
                        .SequenceEqual(dishes.Select(x => (x.Id, x.Description)));

                var isColdCacheFalse = !result.IsColdCache;
                var hasErrorFalse = !result.HasError;

                // Verify PutSavedDishesAsync was called with the correct householdId and matching JSON.
                var putWasCalled = storedJson is not null &&
                    VerifyStoredJson(storedJson, dishes);

                return dishesMatch
                    .Label($"Dishes should match. Expected {dishes.Count} items, got {result.Dishes?.Count ?? 0}")
                    .And(isColdCacheFalse.Label("IsColdCache should be false on successful cold cache fetch"))
                    .And(hasErrorFalse.Label("HasError should be false on successful cold cache fetch"))
                    .And(putWasCalled.Label("PutSavedDishesAsync should be called with correct householdId and JSON"));
            });
    }

    private static BackgroundRefreshResult ExecuteBackgroundRefreshAndVerify(
        List<SavedDishDto> initialList,
        List<SavedDishDto> freshList)
    {
        var householdId = "test-household-id";
        var initialJson = JsonSerializer.Serialize(initialList);
        var freshJson = JsonSerializer.Serialize(freshList);

        var cacheStoreMock = new Mock<ICacheStore>();
        var mutationQueueMock = new Mock<IMutationQueue>();
        var connectivityServiceMock = new Mock<IConnectivityService>();
        var jsRuntimeMock = new Mock<IJSRuntime>();
        var navigationManager = new FakeNavigationManager();

        // Setup: householdId in localStorage.
        jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>(
                "localStorage.getItem",
                It.Is<object[]>(x => x.Length == 1 && x[0].ToString() == "householdId")))
            .ReturnsAsync(householdId);

        // Setup: connectivity is online.
        connectivityServiceMock.Setup(x => x.IsOnline).Returns(true);

        // Setup: cache returns initial list.
        var cachedEntry = new CachedSavedDishes(initialJson, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        cacheStoreMock
            .Setup(x => x.GetSavedDishesAsync(householdId))
            .ReturnsAsync(cachedEntry);

        // Setup: HTTP returns fresh list.
        var handler = new CapturingHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = new StringContent(freshJson, System.Text.Encoding.UTF8, "application/json");
            return response;
        });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/api/") };

        // Track PutSavedDishesAsync calls.
        string? storedJson = null;
        cacheStoreMock
            .Setup(x => x.PutSavedDishesAsync(householdId, It.IsAny<string>()))
            .Callback<string, string>((_, json) => storedJson = json)
            .Returns(Task.CompletedTask);

        var sessionService = new SessionService(jsRuntimeMock.Object, navigationManager, cacheStoreMock.Object);

        var sut = new CachedApiClient(
            cacheStoreMock.Object,
            mutationQueueMock.Object,
            connectivityServiceMock.Object,
            httpClient,
            jsRuntimeMock.Object,
            navigationManager,
            sessionService);

        // Track OnSavedDishesUpdated event.
        IReadOnlyList<SavedDishDto>? eventDishes = null;
        sut.OnSavedDishesUpdated += dishes => eventDishes = dishes;

        // Act: call GetSavedDishesAsync which will return cached and fire background refresh.
        sut.GetSavedDishesAsync().GetAwaiter().GetResult();

        // Wait for the background refresh to complete.
        Thread.Sleep(200);

        // Verify results.
        var putSavedDishesCalled = storedJson is not null;
        var cacheWasUpdatedWithFreshData = storedJson == freshJson;
        var eventWasFired = eventDishes is not null;
        var eventWasFiredWithFreshData = eventWasFired &&
            JsonSerializer.Serialize(eventDishes) == freshJson;

        return new BackgroundRefreshResult(
            putSavedDishesCalled,
            cacheWasUpdatedWithFreshData,
            eventWasFired,
            eventWasFiredWithFreshData);
    }

    private record BackgroundRefreshResult(
        bool PutSavedDishesCalled,
        bool CacheWasUpdatedWithFreshData,
        bool EventWasFired,
        bool EventWasFiredWithFreshData);

    private static bool VerifyStoredJson(string json, List<SavedDishDto> expectedDishes)
    {
        var stored = JsonSerializer.Deserialize<List<SavedDishDto>>(json);
        if (stored is null)
            return expectedDishes.Count == 0;

        return stored.Count == expectedDishes.Count &&
            stored.Select(x => (x.Id, x.Description))
                .SequenceEqual(expectedDishes.Select(x => (x.Id, x.Description)));
    }

    // Feature: saved-dishes-cache, Property 6: Cache invalidation on refetch failure
    // Validates: Requirements 3.2
    [Property(MaxTest = 100)]
    public Property RefreshSavedDishesCacheAsync_RefetchFails_DeletesCacheEntry()
    {
        return Prop.ForAll(
            SavedDishListArb,
            async existingList =>
            {
                // Arrange.
                var householdId = Guid.NewGuid().ToString();
                var existingJson = JsonSerializer.Serialize(existingList);

                var cacheStoreMock = new Mock<ICacheStore>();
                var mutationQueueMock = new Mock<IMutationQueue>();
                var connectivityServiceMock = new Mock<IConnectivityService>();
                var jsRuntimeMock = new Mock<IJSRuntime>();
                var navigationManager = new FakeNavigationManager();

                SetupLocalStorageGetItem(jsRuntimeMock, "householdId", householdId);

                // Setup: cache has an existing entry.
                var cachedEntry = new CachedSavedDishes(existingJson, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                cacheStoreMock
                    .Setup(x => x.GetSavedDishesAsync(householdId))
                    .ReturnsAsync(cachedEntry);

                // Setup: HTTP returns a 500 Internal Server Error (simulating refetch failure).
                var handler = new CapturingHttpMessageHandler(_ =>
                    new HttpResponseMessage(HttpStatusCode.InternalServerError));
                var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/api/") };

                // Track DeleteSavedDishesAsync calls.
                var deleteCalledWithHouseholdId = false;
                cacheStoreMock
                    .Setup(x => x.DeleteSavedDishesAsync(householdId))
                    .Callback(() => deleteCalledWithHouseholdId = true)
                    .Returns(Task.CompletedTask);

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
                await sut.RefreshSavedDishesCacheAsync();

                // Assert.
                return deleteCalledWithHouseholdId
                    .Label($"Expected DeleteSavedDishesAsync to be called with householdId '{householdId}' " +
                           $"when refetch fails. Existing cache had {existingList.Count} items.");
            });
    }

    // Feature: saved-dishes-cache, Property 7: Single entry per household invariant
    // Validates: Requirements 5.1
    [Property(MaxTest = 100)]
    public Property PutSavedDishesAsync_MultipleCallsSameHousehold_CacheContainsAtMostOneEntry()
    {
        var sequenceArb = Gen.Choose(2, 5)
            .SelectMany(count => Gen.ListOf(SavedDishListArb.Generator, count))
            .Select(x => x.ToList())
            .ToArbitrary();

        return Prop.ForAll(
            sequenceArb,
            async dishLists =>
            {
                // Arrange.
                var householdId = Guid.NewGuid().ToString();

                var cacheStoreMock = new Mock<ICacheStore>();
                var mutationQueueMock = new Mock<IMutationQueue>();
                var connectivityServiceMock = new Mock<IConnectivityService>();
                var jsRuntimeMock = new Mock<IJSRuntime>();
                var navigationManager = new FakeNavigationManager();

                connectivityServiceMock.Setup(x => x.IsOnline).Returns(true);
                SetupLocalStorageGetItem(jsRuntimeMock, "householdId", householdId);

                // Track all PutSavedDishesAsync calls to verify overwrite behavior.
                var putCalls = new List<(string HouseholdId, string Json)>();
                cacheStoreMock
                    .Setup(x => x.PutSavedDishesAsync(householdId, It.IsAny<string>()))
                    .Callback<string, string>((hId, json) => putCalls.Add((hId, json)))
                    .Returns(Task.CompletedTask);

                // Start with a cold cache, then simulate sequential puts via RefreshSavedDishesCacheAsync.
                var callIndex = 0;
                cacheStoreMock
                    .Setup(x => x.GetSavedDishesAsync(householdId))
                    .ReturnsAsync((string _) =>
                    {
                        // After first put, return the most recently stored entry.
                        if (putCalls.Count > 0)
                        {
                            var lastPut = putCalls[^1];
                            return new CachedSavedDishes(lastPut.Json, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                        }
                        return null;
                    });

                // Each call to RefreshSavedDishesCacheAsync fetches from API. Cycle through the generated lists.
                var handler = new CapturingHttpMessageHandler(_ =>
                {
                    var index = Interlocked.Increment(ref callIndex) - 1;
                    var listToReturn = dishLists[index % dishLists.Count];
                    var json = JsonSerializer.Serialize(listToReturn);
                    var response = new HttpResponseMessage(HttpStatusCode.OK);
                    response.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                    return response;
                });
                var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/api/") };

                cacheStoreMock.Setup(x => x.DeleteSavedDishesAsync(householdId)).Returns(Task.CompletedTask);

                var sessionService = new SessionService(jsRuntimeMock.Object, navigationManager, cacheStoreMock.Object);

                var sut = new CachedApiClient(
                    cacheStoreMock.Object,
                    mutationQueueMock.Object,
                    connectivityServiceMock.Object,
                    httpClient,
                    jsRuntimeMock.Object,
                    navigationManager,
                    sessionService);

                // Act: call RefreshSavedDishesCacheAsync multiple times (simulates multiple puts).
                for (var i = 0; i < dishLists.Count; i++)
                    await sut.RefreshSavedDishesCacheAsync();

                // Assert: every PutSavedDishesAsync call was for the same householdId (single key).
                var allSameHousehold = putCalls.All(x => x.HouseholdId == householdId);

                // Assert: the last stored value matches the last API response (overwrite, not accumulate).
                var lastExpectedJson = JsonSerializer.Serialize(dishLists[^1]);
                var lastStoredJson = putCalls.Count > 0 ? putCalls[^1].Json : null;
                var lastStoreMatchesLastResponse = lastStoredJson == lastExpectedJson;

                // Assert: PutSavedDishesAsync was called exactly once per refresh (not accumulating entries).
                var putCountMatchesRefreshCount = putCalls.Count == dishLists.Count;

                return allSameHousehold
                    .Label($"All puts should target the same householdId. " +
                           $"Distinct householdIds: {putCalls.Select(x => x.HouseholdId).Distinct().Count()}")
                    .And(lastStoreMatchesLastResponse
                        .Label($"Last stored JSON should match last API response. " +
                               $"Lists count: {dishLists.Count}, Put calls: {putCalls.Count}"))
                    .And(putCountMatchesRefreshCount
                        .Label($"Put count ({putCalls.Count}) should equal refresh count ({dishLists.Count}) — " +
                               $"each refresh overwrites the single entry, not adds a new one"));
            });
    }

    private static void SetupLocalStorageGetItem(Mock<IJSRuntime> jsRuntimeMock, string key, string? value)
    {
        jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>(
                "localStorage.getItem",
                It.Is<object[]>(x => x.Length == 1 && x[0].ToString() == key)))
            .ReturnsAsync(value);
    }
}
