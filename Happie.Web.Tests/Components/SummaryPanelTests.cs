using System.Net;
using System.Net.Http.Json;
using Bunit;
using Happie.Shared.Contracts;
using Happie.Web.Components;
using Happie.Web.Resources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using RichardSzalay.MockHttp;

namespace Happie.Web.Tests.Components;

public class SummaryPanelTests : BunitContext
{
    private readonly MockHttpMessageHandler _mockHttp = new();
    private readonly Mock<IStringLocalizer<AppStrings>> _localizerMock = new();

    private static readonly Guid TestDishId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    public SummaryPanelTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        SetupLocalizer();

        var httpClient = _mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("http://localhost/api/");
        Services.AddSingleton(httpClient);
        Services.AddSingleton(_localizerMock.Object);
    }

    [Fact]
    public void Render_AllFieldsEmpty_DisplaysPlaceholderText()
    {
        // Arrange.
        SetupSummaryEndpoint(null, null, null);

        // Act.
        var cut = Render<SummaryPanel>(parameters => parameters
            .Add(x => x.SavedDishId, TestDishId));

        cut.WaitForState(() => cut.FindAll(".summary-panel__empty").Count > 0, TimeSpan.FromSeconds(5));

        // Assert.
        var placeholder = cut.Find(".summary-panel__empty");
        Assert.Equal("DishDetails_SummaryEmpty", placeholder.TextContent);
    }

    [Fact]
    public void Render_WithData_DisplaysSummaryTextDurationAndServings()
    {
        // Arrange.
        SetupSummaryEndpoint("A delicious pasta dish", 90, 4);

        // Act.
        var cut = Render<SummaryPanel>(parameters => parameters
            .Add(x => x.SavedDishId, TestDishId));

        cut.WaitForState(() => cut.FindAll(".summary-panel__text").Count > 0, TimeSpan.FromSeconds(5));

        // Assert.
        var summaryText = cut.Find(".summary-panel__text");
        Assert.Equal("A delicious pasta dish", summaryText.TextContent);

        var metaValues = cut.FindAll(".summary-panel__meta-value");
        Assert.Equal("01:30", metaValues[0].TextContent);
        Assert.Equal("4", metaValues[1].TextContent);
    }

    [Fact]
    public void EditIcon_Click_SwitchesToEditModeWithTextareaAndInputs()
    {
        // Arrange.
        SetupSummaryEndpoint("Test summary", 60, 2);

        var cut = Render<SummaryPanel>(parameters => parameters
            .Add(x => x.SavedDishId, TestDishId));

        cut.WaitForState(() => cut.FindAll(".summary-panel__text").Count > 0, TimeSpan.FromSeconds(5));

        // Act.
        var editButton = cut.Find("[aria-label='DishDetails_Edit']");
        editButton.Click();

        // Assert.
        Assert.NotEmpty(cut.FindAll(".summary-panel__textarea"));
        Assert.NotEmpty(cut.FindAll(".summary-panel__time-input"));
        Assert.NotEmpty(cut.FindAll(".summary-panel__servings-input"));
    }

    [Fact]
    public void Discard_RevertsToReadModeWithoutChanges()
    {
        // Arrange.
        SetupSummaryEndpoint("Original text", 45, 3);

        var cut = Render<SummaryPanel>(parameters => parameters
            .Add(x => x.SavedDishId, TestDishId));

        cut.WaitForState(() => cut.FindAll(".summary-panel__text").Count > 0, TimeSpan.FromSeconds(5));

        // Enter edit mode.
        cut.Find("[aria-label='DishDetails_Edit']").Click();

        // Act — discard.
        cut.Find("[aria-label='DishDetails_Discard']").Click();

        // Assert — back in read mode with original text.
        Assert.NotEmpty(cut.FindAll(".summary-panel__text"));
        Assert.Equal("Original text", cut.Find(".summary-panel__text").TextContent);
        Assert.Empty(cut.FindAll(".summary-panel__textarea"));
    }

    private void SetupSummaryEndpoint(string? summary, int? durationMinutes, int? servings)
    {
        var response = new RecipeSummaryResponse(summary, durationMinutes, servings);
        _mockHttp
            .When(HttpMethod.Get, "http://localhost/api/saved-dishes/*/summary")
            .Respond("application/json", System.Text.Json.JsonSerializer.Serialize(response));
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
