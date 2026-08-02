using Bunit;
using Happie.Shared.Contracts;
using Happie.Web.Components;
using Happie.Web.Resources;
using Happie.Web.Services;
using Happie.Web.Services.Caching;
using Happie.Web.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;

namespace Happie.Web.Tests.Components;

public class DishPanelAutocompleteTests : BunitContext
{
    private readonly Mock<ICachedApiClient> _cachedApiMock = new();
    private readonly Mock<IStringLocalizer<AppStrings>> _localizerMock = new();

    private static readonly List<SavedDishDto> TestDishes =
    [
        new(Guid.NewGuid(), "Pizza Margherita"),
        new(Guid.NewGuid(), "Pasta Bolognese"),
        new(Guid.NewGuid(), "Spaghetti Carbonara")
    ];

    public DishPanelAutocompleteTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        SetupLocalizer();
        SetupCachedApiWithDishes(TestDishes);

        Services.AddSingleton(_cachedApiMock.Object);
        Services.AddSingleton(_localizerMock.Object);

        // Default: cursor is at end.
        JSInterop.Setup<bool>("happie.getCursorAtEnd", _ => true).SetResult(true);
    }

    [Fact]
    public void Render_WithMatchingSuggestion_ShowsGhostText()
    {
        // Arrange.
        var cut = RenderDishPanelInEditMode();

        // Act.
        var input = cut.Find("input.dish-panel__input");
        input.Input("Piz");

        // Assert.
        var ghostText = cut.Find("span.dish-panel__ghost-text-suggestion");
        Assert.Equal("za Margherita", ghostText.TextContent);
    }

    [Fact]
    public void HandleDishBlur_WhenGhostTextVisible_HidesGhostText()
    {
        // Arrange.
        var cut = RenderDishPanelInEditMode();
        var input = cut.Find("input.dish-panel__input");
        input.Input("Piz");

        // Verify ghost text is visible first.
        Assert.NotEmpty(cut.FindAll("span.dish-panel__ghost-text-suggestion"));

        // Act.
        input.Blur();

        // Assert.
        Assert.Empty(cut.FindAll("span.dish-panel__ghost-text-suggestion"));
    }

    [Fact]
    public void HandleDishInput_CursorNotAtEnd_HidesGhostText()
    {
        // Arrange.
        JSInterop.Setup<bool>("happie.getCursorAtEnd", _ => true).SetResult(false);
        var cut = RenderDishPanelInEditMode();

        // Act.
        var input = cut.Find("input.dish-panel__input");
        input.Input("Piz");

        // Assert.
        Assert.Empty(cut.FindAll("span.dish-panel__ghost-text-suggestion"));
    }

    [Fact]
    public void HandleKeyDown_TabWithGhostText_AcceptsSuggestion()
    {
        // Arrange.
        var cut = RenderDishPanelInEditMode();
        var input = cut.Find("input.dish-panel__input");
        input.Input("Piz");

        // Verify ghost text is present.
        Assert.NotEmpty(cut.FindAll("span.dish-panel__ghost-text-suggestion"));

        // Act.
        input.KeyDown(new KeyboardEventArgs { Key = "Tab" });

        // Assert.
        Assert.Empty(cut.FindAll("span.dish-panel__ghost-text-suggestion"));
        var updatedInput = cut.Find("input.dish-panel__input");
        Assert.Equal("Pizza Margherita", updatedInput.GetAttribute("value"));
    }

    [Fact]
    public void HandleKeyDown_RightArrowAtEnd_AcceptsSuggestion()
    {
        // Arrange.
        JSInterop.Setup<bool>("happie.getCursorAtEnd", _ => true).SetResult(true);
        var cut = RenderDishPanelInEditMode();
        var input = cut.Find("input.dish-panel__input");
        input.Input("Piz");

        // Verify ghost text is present.
        Assert.NotEmpty(cut.FindAll("span.dish-panel__ghost-text-suggestion"));

        // Act.
        input.KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });

        // Assert.
        Assert.Empty(cut.FindAll("span.dish-panel__ghost-text-suggestion"));
        var updatedInput = cut.Find("input.dish-panel__input");
        Assert.Equal("Pizza Margherita", updatedInput.GetAttribute("value"));
    }

    [Fact]
    public void HandleKeyDown_RightArrowNotAtEnd_DoesNotAccept()
    {
        // Arrange — cursor not at end from the start, so ghost text never appears.
        JSInterop.Setup<bool>("happie.getCursorAtEnd", _ => true).SetResult(false);
        var cut = RenderDishPanelInEditMode();
        var input = cut.Find("input.dish-panel__input");
        input.Input("Piz");

        // Ghost text is hidden because cursor is not at end.
        Assert.Empty(cut.FindAll("span.dish-panel__ghost-text-suggestion"));

        // Act — press Right arrow; should not accept any suggestion.
        input.KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });

        // Assert — input value unchanged, no ghost text.
        var updatedInput = cut.Find("input.dish-panel__input");
        Assert.Equal("Piz", updatedInput.GetAttribute("value"));
        Assert.Empty(cut.FindAll("span.dish-panel__ghost-text-suggestion"));
    }

    [Fact]
    public void HandleGhostTextTap_WhenGhostTextVisible_AcceptsSuggestion()
    {
        // Arrange.
        var cut = RenderDishPanelInEditMode();
        var input = cut.Find("input.dish-panel__input");
        input.Input("Piz");

        // Verify ghost text is present.
        Assert.NotEmpty(cut.FindAll("span.dish-panel__ghost-text-suggestion"));

        // Act.
        var tapTarget = cut.Find("span.dish-panel__ghost-text-suggestion");
        tapTarget.MouseDown();

        // Assert.
        Assert.Empty(cut.FindAll("span.dish-panel__ghost-text-suggestion"));
        var updatedInput = cut.Find("input.dish-panel__input");
        Assert.Equal("Pizza Margherita", updatedInput.GetAttribute("value"));
    }

    [Fact]
    public void Render_InSavedMode_NoGhostText()
    {
        // Arrange — render with committed saved dish IDs to start in saved mode.
        var savedDishId = TestDishes[0].Id;
        var cut = Render<DishPanel>(parameters => parameters
            .Add(x => x.Date, "2025-01-15")
            .Add(x => x.Dish, new DishDto("Pizza Margherita", null, null, null, null, new List<Guid> { savedDishId }))
            .Add(x => x.Attendance, new List<AttendanceDto>())
            .Add(x => x.ActingHousemateId, Guid.NewGuid())
            .Add(x => x.OnDishChanged, EventCallback.Factory.Create<string?>(this, _ => { }))
            .Add(x => x.SavedDishModalRef, null));

        // Act — enter edit mode (will be in saved mode due to committed saved dish IDs).
        cut.Find("button.dish-panel__edit-btn").Click();

        // Assert — no ghost text in saved mode.
        Assert.Empty(cut.FindAll("span.dish-panel__ghost-text-suggestion"));
    }

    [Fact]
    public void HandleCustomMode_SwitchFromSaved_ShowsGhostText()
    {
        // Arrange — render with committed saved dish IDs to start in saved mode.
        var savedDishId = TestDishes[0].Id;
        var cut = Render<DishPanel>(parameters => parameters
            .Add(x => x.Date, "2025-01-15")
            .Add(x => x.Dish, new DishDto("Pizza Margherita", null, null, null, null, new List<Guid> { savedDishId }))
            .Add(x => x.Attendance, new List<AttendanceDto>())
            .Add(x => x.ActingHousemateId, Guid.NewGuid())
            .Add(x => x.OnDishChanged, EventCallback.Factory.Create<string?>(this, _ => { }))
            .Add(x => x.SavedDishModalRef, null));

        // Enter edit mode (saved mode).
        cut.Find("button.dish-panel__edit-btn").Click();

        // Verify no ghost text in saved mode.
        Assert.Empty(cut.FindAll("span.dish-panel__ghost-text-suggestion"));

        // Act — switch to custom mode with a prefix that matches a saved dish.
        cut.InvokeAsync(() => cut.Instance.HandleCustomMode("Spa"));

        // Assert — ghost text should appear for "Spaghetti Carbonara".
        var ghostText = cut.Find("span.dish-panel__ghost-text-suggestion");
        Assert.Equal("ghetti Carbonara", ghostText.TextContent);
    }

    private IRenderedComponent<DishPanel> RenderDishPanelInEditMode()
    {
        var cut = Render<DishPanel>(parameters => parameters
            .Add(x => x.Date, "2025-01-15")
            .Add(x => x.Dish, null)
            .Add(x => x.Attendance, new List<AttendanceDto>())
            .Add(x => x.ActingHousemateId, Guid.NewGuid())
            .Add(x => x.OnDishChanged, EventCallback.Factory.Create<string?>(this, _ => { }))
            .Add(x => x.SavedDishModalRef, null));

        // Enter edit mode.
        cut.Find("button.dish-panel__edit-btn").Click();
        return cut;
    }

    private void SetupCachedApiWithDishes(List<SavedDishDto> dishes)
    {
        _cachedApiMock
            .Setup(x => x.GetSavedDishesAsync())
            .ReturnsAsync(new SavedDishesFetchResult(dishes, false, false));
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
