using System.Net;
using System.Text.Json;
using Bunit;
using Bunit.TestDoubles;
using Happie.Shared.Contracts;
using Happie.Web.Pages;
using Happie.Web.Resources;
using Happie.Web.Services;
using Happie.Web.Services.Caching;
using Happie.Web.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using Moq;

namespace Happie.Web.Tests.Services.Caching;

public class CachedApiClientSavedDishesFallbackTests : BunitContext
{
    private readonly Mock<ICacheStore> _cacheStoreMock = new();
    private readonly Mock<IMutationQueue> _mutationQueueMock = new();
    private readonly Mock<IConnectivityService> _connectivityServiceMock = new();
    private readonly Mock<IJSRuntime> _jsRuntimeMock = new();
    private readonly FakeNavigationManager _navigationManager = new();
    private readonly Mock<IStringLocalizer<AppStrings>> _localizerMock = new();

    private const string HouseholdId = "test-household-id";

    public CachedApiClientSavedDishesFallbackTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        _localizerMock
            .Setup(x => x[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key));
        _localizerMock
            .Setup(x => x[It.IsAny<string>(), It.IsAny<object[]>()])
            .Returns((string key, object[] _) => new LocalizedString(key, key));
    }

    [Fact]
    public async Task GetSavedDishesAsync_IndexedDbUnavailable_FallsBackToApiCalls()
    {
        // Arrange.
        var dishes = new List<SavedDishDto>
        {
            new(Guid.NewGuid(), "Pasta Carbonara"),
            new(Guid.NewGuid(), "Risotto"),
        };
        var responseJson = JsonSerializer.Serialize(dishes);

        // Cache returns null (simulating IndexedDB unavailable).
        _cacheStoreMock
            .Setup(x => x.GetSavedDishesAsync(HouseholdId))
            .ReturnsAsync((CachedSavedDishes?)null);

        SetupConnectivityOnline();
        SetupLocalStorageGetItem("householdId", HouseholdId);

        // HTTP returns a valid list.
        var handler = new CapturingHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json");
            return response;
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

        // Act.
        var result = await sut.GetSavedDishesAsync();

        // Assert.
        Assert.NotNull(result.Dishes);
        Assert.Equal(2, result.Dishes.Count);
        Assert.Equal("Pasta Carbonara", result.Dishes[0].Description);
        Assert.Equal("Risotto", result.Dishes[1].Description);
        Assert.False(result.IsColdCache);
        Assert.False(result.HasError);
    }

    [Fact]
    public void Render_Offline_DisablesMutationButtons()
    {
        // Arrange.
        var dishes = new List<SavedDishDto>
        {
            new(Guid.NewGuid(), "Pasta"),
            new(Guid.NewGuid(), "Risotto"),
        };

        var cachedApiMock = new Mock<ICachedApiClient>();
        cachedApiMock
            .Setup(x => x.GetSavedDishesAsync())
            .ReturnsAsync(new SavedDishesFetchResult(dishes, false, false));

        _connectivityServiceMock.Setup(x => x.IsOnline).Returns(false);

        Services.AddSingleton(cachedApiMock.Object);
        Services.AddSingleton(_connectivityServiceMock.Object);
        Services.AddSingleton(_localizerMock.Object);
        this.RegisterHttpClient(HttpStatusCode.OK, new List<SavedDishSuggestionDto>());

        // Act.
        var cut = Render<SavedDishesPage>();

        // Assert.
        var addButton = cut.Find(".saved-dishes-page__add-btn");
        Assert.True(addButton.HasAttribute("disabled"));

        var editButtons = cut.FindAll(".saved-dishes-page__icon-btn");
        Assert.True(editButtons.Count >= 2);
        foreach (var button in editButtons)
            Assert.True(button.HasAttribute("disabled"));
    }

    private void SetupConnectivityOnline()
    {
        _connectivityServiceMock.Setup(x => x.IsOnline).Returns(true);
    }

    private void SetupLocalStorageGetItem(string key, string? value)
    {
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>(
                "localStorage.getItem",
                It.Is<object[]>(x => x.Length == 1 && x[0].ToString() == key)))
            .ReturnsAsync(value);
    }
}
