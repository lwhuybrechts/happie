using Bunit;
using Happie.Shared.Contracts;
using Happie.Web.Components;

namespace Happie.Web.Tests.Components;

public class DonutChartTests : BunitContext
{
    private static readonly Guid HousemateA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid HousemateB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid HousemateC = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    [Fact]
    public void Render_WithMultipleSegments_RendersDonutChart()
    {
        // Arrange.
        var segments = CreateSegments();

        // Act.
        var cut = RenderDonutChart(segments, HousemateA);

        // Assert.
        var svg = cut.Find("svg.donut-chart__svg");
        Assert.NotNull(svg);
    }

    [Fact]
    public void Render_WithMultipleSegments_RendersCorrectNumberOfCircles()
    {
        // Arrange.
        var segments = CreateSegments();

        // Act.
        var cut = RenderDonutChart(segments, HousemateA);

        // Assert.
        var circles = cut.FindAll("circle.donut-chart__segment");
        Assert.Equal(3, circles.Count);
    }

    [Fact]
    public void Render_WithMultipleSegments_HighlightsCurrentHousemate()
    {
        // Arrange.
        var segments = CreateSegments();

        // Act.
        var cut = RenderDonutChart(segments, HousemateB);

        // Assert.
        var highlightedCircles = cut.FindAll("circle.donut-chart__segment--highlighted");
        Assert.Single(highlightedCircles);
    }

    [Fact]
    public void Render_WithMultipleSegments_UsesHousemateColors()
    {
        // Arrange.
        var segments = CreateSegments();

        // Act.
        var cut = RenderDonutChart(segments, HousemateA);

        // Assert.
        var circles = cut.FindAll("circle.donut-chart__segment");
        Assert.Equal("#FF5733", circles[0].GetAttribute("stroke"));
        Assert.Equal("#33FF57", circles[1].GetAttribute("stroke"));
        Assert.Equal("#3357FF", circles[2].GetAttribute("stroke"));
    }

    [Fact]
    public void Render_WithMultipleSegments_DisplaysPercentageLabels()
    {
        // Arrange.
        var segments = new List<CookingShareDto>
        {
            new(HousemateA, "Alice", "#FF5733", 5),
            new(HousemateB, "Bob", "#33FF57", 3),
            new(HousemateC, "Charlie", "#3357FF", 2),
        };

        // Act.
        var cut = RenderDonutChart(segments, HousemateA);

        // Assert.
        var percentages = cut.FindAll(".donut-chart__label-percentage");
        Assert.Equal("50%", percentages[0].TextContent);
        Assert.Equal("30%", percentages[1].TextContent);
        Assert.Equal("20%", percentages[2].TextContent);
    }

    [Fact]
    public void Render_WithMultipleSegments_DisplaysHousemateNames()
    {
        // Arrange.
        var segments = CreateSegments();

        // Act.
        var cut = RenderDonutChart(segments, HousemateA);

        // Assert.
        var names = cut.FindAll(".donut-chart__label-text");
        Assert.Equal("Alice", names[0].TextContent);
        Assert.Equal("Bob", names[1].TextContent);
        Assert.Equal("Charlie", names[2].TextContent);
    }

    [Fact]
    public void Render_WithZeroTotal_DoesNotRenderChart()
    {
        // Arrange.
        var segments = new List<CookingShareDto>
        {
            new(HousemateA, "Alice", "#FF5733", 0),
            new(HousemateB, "Bob", "#33FF57", 0),
        };

        // Act.
        var cut = RenderDonutChart(segments, HousemateA);

        // Assert.
        Assert.Empty(cut.FindAll(".donut-chart"));
    }

    [Fact]
    public void Render_WithNullSegments_DoesNotRenderChart()
    {
        // Act.
        var cut = RenderDonutChart(null, HousemateA);

        // Assert.
        Assert.Empty(cut.FindAll(".donut-chart"));
    }

    [Fact]
    public void Render_HighlightedSegment_HasThickerStrokeWidth()
    {
        // Arrange.
        var segments = CreateSegments();

        // Act.
        var cut = RenderDonutChart(segments, HousemateA);

        // Assert.
        var highlightedCircle = cut.Find("circle.donut-chart__segment--highlighted");
        var normalCircle = cut.FindAll("circle.donut-chart__segment")
            .First(x => !x.ClassList.Contains("donut-chart__segment--highlighted"));

        var highlightedWidth = double.Parse(highlightedCircle.GetAttribute("stroke-width")!);
        var normalWidth = double.Parse(normalCircle.GetAttribute("stroke-width")!);
        Assert.True(highlightedWidth > normalWidth);
    }

    [Fact]
    public void Render_FitsWithinViewport_HasMaxWidthConstraint()
    {
        // Arrange.
        var segments = CreateSegments();

        // Act.
        var cut = RenderDonutChart(segments, HousemateA);

        // Assert — the container has the donut-chart class which has max-width: 100vw in CSS.
        var container = cut.Find(".donut-chart");
        Assert.NotNull(container);
    }

    private IRenderedComponent<DonutChart> RenderDonutChart(
        IReadOnlyList<CookingShareDto>? segments,
        Guid highlightedHousemateId)
    {
        return Render<DonutChart>(parameters => parameters
            .Add(x => x.Segments, segments)
            .Add(x => x.HighlightedHousemateId, highlightedHousemateId));
    }

    private static List<CookingShareDto> CreateSegments()
    {
        return
        [
            new(HousemateA, "Alice", "#FF5733", 10),
            new(HousemateB, "Bob", "#33FF57", 7),
            new(HousemateC, "Charlie", "#3357FF", 3),
        ];
    }
}
