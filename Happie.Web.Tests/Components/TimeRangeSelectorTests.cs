using Bunit;
using Happie.Web.Components;
using Happie.Web.Resources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;

namespace Happie.Web.Tests.Components;

public class TimeRangeSelectorTests : BunitContext
{
    private readonly Mock<IStringLocalizer<AppStrings>> _localizerMock = new();

    public TimeRangeSelectorTests()
    {
        SetupLocalizer();
        Services.AddSingleton(_localizerMock.Object);
    }

    [Fact]
    public void Render_DefaultState_RendersFourPills()
    {
        // Act.
        var cut = RenderTimeRangeSelector(TimeRange.ThirtyDays);

        // Assert.
        var pills = cut.FindAll(".time-range-selector__pill");
        Assert.Equal(4, pills.Count);
    }

    [Fact]
    public void Render_DefaultState_DisplaysCorrectLabels()
    {
        // Act.
        var cut = RenderTimeRangeSelector(TimeRange.ThirtyDays);

        // Assert.
        var pills = cut.FindAll(".time-range-selector__pill");
        Assert.Equal("TimeRange_ThirtyDays", pills[0].TextContent);
        Assert.Equal("TimeRange_ThreeMonths", pills[1].TextContent);
        Assert.Equal("TimeRange_OneYear", pills[2].TextContent);
        Assert.Equal("TimeRange_AllTime", pills[3].TextContent);
    }

    [Fact]
    public void Render_ThirtyDaysSelected_FirstPillHasActiveClass()
    {
        // Act.
        var cut = RenderTimeRangeSelector(TimeRange.ThirtyDays);

        // Assert.
        var pills = cut.FindAll(".time-range-selector__pill");
        Assert.Contains("time-range-selector__pill--active", pills[0].ClassList);
        Assert.DoesNotContain("time-range-selector__pill--active", pills[1].ClassList);
        Assert.DoesNotContain("time-range-selector__pill--active", pills[2].ClassList);
        Assert.DoesNotContain("time-range-selector__pill--active", pills[3].ClassList);
    }

    [Fact]
    public void Render_AllTimeSelected_LastPillHasActiveClass()
    {
        // Act.
        var cut = RenderTimeRangeSelector(TimeRange.AllTime);

        // Assert.
        var pills = cut.FindAll(".time-range-selector__pill");
        Assert.DoesNotContain("time-range-selector__pill--active", pills[0].ClassList);
        Assert.DoesNotContain("time-range-selector__pill--active", pills[1].ClassList);
        Assert.DoesNotContain("time-range-selector__pill--active", pills[2].ClassList);
        Assert.Contains("time-range-selector__pill--active", pills[3].ClassList);
    }

    [Fact]
    public void Render_ActivePill_HasAriaPressedTrue()
    {
        // Act.
        var cut = RenderTimeRangeSelector(TimeRange.ThreeMonths);

        // Assert.
        var pills = cut.FindAll(".time-range-selector__pill");
        Assert.Equal("false", pills[0].GetAttribute("aria-pressed"));
        Assert.Equal("true", pills[1].GetAttribute("aria-pressed"));
        Assert.Equal("false", pills[2].GetAttribute("aria-pressed"));
        Assert.Equal("false", pills[3].GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Click_Pill_InvokesOnRangeSelectedCallback()
    {
        // Arrange.
        TimeRange? selectedRange = null;
        var cut = Render<TimeRangeSelector>(parameters => parameters
            .Add(x => x.SelectedRange, TimeRange.ThirtyDays)
            .Add(x => x.OnRangeSelected, (TimeRange range) => { selectedRange = range; return Task.CompletedTask; }));

        // Act.
        var pills = cut.FindAll(".time-range-selector__pill");
        pills[2].Click();

        // Assert.
        Assert.Equal(TimeRange.OneYear, selectedRange);
    }

    [Fact]
    public void Render_HasRoleGroupAttribute()
    {
        // Act.
        var cut = RenderTimeRangeSelector(TimeRange.ThirtyDays);

        // Assert.
        var container = cut.Find(".time-range-selector");
        Assert.Equal("group", container.GetAttribute("role"));
    }

    private IRenderedComponent<TimeRangeSelector> RenderTimeRangeSelector(TimeRange selectedRange)
    {
        return Render<TimeRangeSelector>(parameters => parameters
            .Add(x => x.SelectedRange, selectedRange)
            .Add(x => x.OnRangeSelected, (TimeRange _) => Task.CompletedTask));
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
