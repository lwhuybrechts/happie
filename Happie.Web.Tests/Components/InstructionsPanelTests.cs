using System.Net;
using Bunit;
using Happie.Shared.Contracts;
using Happie.Web.Components;
using Happie.Web.Resources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using RichardSzalay.MockHttp;

namespace Happie.Web.Tests.Components;

public class InstructionsPanelTests : BunitContext
{
    private readonly MockHttpMessageHandler _mockHttp = new();
    private readonly Mock<IStringLocalizer<AppStrings>> _localizerMock = new();

    private static readonly Guid TestDishId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    public InstructionsPanelTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        SetupLocalizer();

        var httpClient = _mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("http://localhost/api/");
        Services.AddSingleton(httpClient);
        Services.AddSingleton(_localizerMock.Object);
    }

    [Fact]
    public void Render_NoInstructions_DisplaysPlaceholderText()
    {
        // Arrange.
        SetupInstructionsEndpoint(new List<CookingInstructionDto>());

        // Act.
        var cut = Render<InstructionsPanel>(parameters => parameters
            .Add(x => x.SavedDishId, TestDishId));

        cut.WaitForState(() => cut.FindAll(".instructions-panel__empty").Count > 0, TimeSpan.FromSeconds(5));

        // Assert.
        var placeholder = cut.Find(".instructions-panel__empty");
        Assert.Equal("DishDetails_InstructionsEmpty", placeholder.TextContent);
    }

    [Fact]
    public void Render_WithInstructions_DisplaysNumberedListInReadMode()
    {
        // Arrange.
        var instructions = new List<CookingInstructionDto>
        {
            new(Guid.NewGuid(), "Boil the water", 0),
            new(Guid.NewGuid(), "Add pasta to the pot", 1),
            new(Guid.NewGuid(), "Drain and serve", 2),
        };
        SetupInstructionsEndpoint(instructions);

        // Act.
        var cut = Render<InstructionsPanel>(parameters => parameters
            .Add(x => x.SavedDishId, TestDishId));

        cut.WaitForState(() => cut.FindAll(".instructions-panel__item").Count > 0, TimeSpan.FromSeconds(5));

        // Assert — rendered as an ordered list with 3 items.
        var orderedList = cut.Find("ol.instructions-panel__list");
        Assert.NotNull(orderedList);

        var items = cut.FindAll(".instructions-panel__item");
        Assert.Equal(3, items.Count);

        var texts = cut.FindAll(".instructions-panel__text");
        Assert.Equal("Boil the water", texts[0].TextContent);
        Assert.Equal("Add pasta to the pot", texts[1].TextContent);
        Assert.Equal("Drain and serve", texts[2].TextContent);
    }

    [Fact]
    public void EditMode_ShowsTextareasAndActionButtons()
    {
        // Arrange.
        var instructions = new List<CookingInstructionDto>
        {
            new(Guid.NewGuid(), "Step one", 0),
            new(Guid.NewGuid(), "Step two", 1),
        };
        SetupInstructionsEndpoint(instructions);

        var cut = Render<InstructionsPanel>(parameters => parameters
            .Add(x => x.SavedDishId, TestDishId));

        cut.WaitForState(() => cut.FindAll(".instructions-panel__item").Count > 0, TimeSpan.FromSeconds(5));

        // Act — click edit button.
        var editButton = cut.Find("[aria-label='DishDetails_Edit']");
        editButton.Click();

        // Assert — textareas present for each instruction.
        var textareas = cut.FindAll(".instructions-panel__textarea");
        Assert.Equal(2, textareas.Count);

        // Assert — confirm and discard buttons present.
        Assert.NotEmpty(cut.FindAll("[aria-label='DishDetails_Confirm']"));
        Assert.NotEmpty(cut.FindAll("[aria-label='DishDetails_Discard']"));

        // Assert — reorder and delete controls present.
        Assert.NotEmpty(cut.FindAll("[aria-label='DishDetails_MoveUp']"));
        Assert.NotEmpty(cut.FindAll("[aria-label='DishDetails_MoveDown']"));
        Assert.NotEmpty(cut.FindAll("[aria-label='DishDetails_DeleteInstruction']"));

        // Assert — add button present.
        Assert.NotEmpty(cut.FindAll("[aria-label='DishDetails_AddInstruction']"));
    }

    private void SetupInstructionsEndpoint(List<CookingInstructionDto> instructions)
    {
        var response = new InstructionsResponse(instructions);
        _mockHttp
            .When(HttpMethod.Get, "http://localhost/api/saved-dishes/*/instructions")
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
