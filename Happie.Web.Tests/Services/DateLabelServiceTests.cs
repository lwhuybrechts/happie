using System.Globalization;
using ExpectedObjects;
using Happie.Web.Services;

namespace Happie.Web.Tests.Services;

public class DateLabelServiceTests
{
    private static readonly CultureInfo EnglishCulture = CultureInfo.GetCultureInfo("en");
    private static readonly CultureInfo DutchCulture = CultureInfo.GetCultureInfo("nl");

    [Fact]
    public void GetLabel_Today_ReturnsTodayTitle()
    {
        // Arrange.
        var today = new DateOnly(2025, 6, 18);

        // Act.
        var result = DateLabelService.GetLabel(today, today, EnglishCulture);

        // Assert.
        new DateLabel("Today", "18 Jun 2025", TitleIsBold: true, DateIsBold: false)
            .ToExpectedObject()
            .ShouldEqual(result);
    }

    [Fact]
    public void GetLabel_Yesterday_ReturnsYesterdayTitle()
    {
        // Arrange.
        var today = new DateOnly(2025, 6, 18);
        var yesterday = new DateOnly(2025, 6, 17);

        // Act.
        var result = DateLabelService.GetLabel(yesterday, today, EnglishCulture);

        // Assert.
        new DateLabel("Yesterday", "17 Jun 2025", TitleIsBold: true, DateIsBold: false)
            .ToExpectedObject()
            .ShouldEqual(result);
    }

    [Fact]
    public void GetLabel_Tomorrow_ReturnsTomorrowTitle()
    {
        // Arrange.
        var today = new DateOnly(2025, 6, 18);
        var tomorrow = new DateOnly(2025, 6, 19);

        // Act.
        var result = DateLabelService.GetLabel(tomorrow, today, EnglishCulture);

        // Assert.
        new DateLabel("Tomorrow", "19 Jun 2025", TitleIsBold: true, DateIsBold: false)
            .ToExpectedObject()
            .ShouldEqual(result);
    }

    [Fact]
    public void GetLabel_ThreeDaysAgo_ReturnsDayName()
    {
        // Arrange.
        var today = new DateOnly(2025, 6, 18);
        var threeDaysAgo = new DateOnly(2025, 6, 15);

        // Act.
        var result = DateLabelService.GetLabel(threeDaysAgo, today, EnglishCulture);

        // Assert.
        new DateLabel("Sunday", "15 Jun 2025", TitleIsBold: true, DateIsBold: false)
            .ToExpectedObject()
            .ShouldEqual(result);
    }

    [Fact]
    public void GetLabel_SixDaysInFuture_ReturnsDayName()
    {
        // Arrange.
        var today = new DateOnly(2025, 6, 18);
        var sixDaysAhead = new DateOnly(2025, 6, 24);

        // Act.
        var result = DateLabelService.GetLabel(sixDaysAhead, today, EnglishCulture);

        // Assert.
        new DateLabel("Tuesday", "24 Jun 2025", TitleIsBold: true, DateIsBold: false)
            .ToExpectedObject()
            .ShouldEqual(result);
    }

    [Fact]
    public void GetLabel_SevenDaysAgo_ReturnsNullTitleWithBoldDate()
    {
        // Arrange.
        var today = new DateOnly(2025, 6, 18);
        var sevenDaysAgo = new DateOnly(2025, 6, 11);

        // Act.
        var result = DateLabelService.GetLabel(sevenDaysAgo, today, EnglishCulture);

        // Assert.
        new DateLabel(null, "11 Jun 2025", TitleIsBold: false, DateIsBold: true)
            .ToExpectedObject()
            .ShouldEqual(result);
    }

    [Fact]
    public void GetLabel_ThirtyDaysInFuture_ReturnsNullTitleWithBoldDate()
    {
        // Arrange.
        var today = new DateOnly(2025, 6, 18);
        var thirtyDaysAhead = new DateOnly(2025, 7, 18);

        // Act.
        var result = DateLabelService.GetLabel(thirtyDaysAhead, today, EnglishCulture);

        // Assert.
        new DateLabel(null, "18 Jul 2025", TitleIsBold: false, DateIsBold: true)
            .ToExpectedObject()
            .ShouldEqual(result);
    }

    [Fact]
    public void GetLabel_DutchLocale_UsesLocalizedMonthAbbreviation()
    {
        // Arrange.
        var today = new DateOnly(2025, 6, 18);

        // Act.
        var result = DateLabelService.GetLabel(today, today, DutchCulture);

        // Assert.
        var expectedMonth = today.ToString("MMM", DutchCulture);
        Assert.Contains(expectedMonth, result.FormattedDate);
    }

    [Fact]
    public void GetLabel_DutchLocale_ThreeDaysAgo_UsesLocalizedDayName()
    {
        // Arrange.
        var today = new DateOnly(2025, 6, 18);
        var threeDaysAgo = new DateOnly(2025, 6, 15);

        // Act.
        var result = DateLabelService.GetLabel(threeDaysAgo, today, DutchCulture);

        // Assert.
        var expectedDayName = threeDaysAgo.ToString("dddd", DutchCulture);
        Assert.Equal(expectedDayName, result.Title);
    }

    [Fact]
    public void GetLabel_TwoDaysAgo_ReturnsDayName()
    {
        // Arrange.
        var today = new DateOnly(2025, 6, 18);
        var twoDaysAgo = new DateOnly(2025, 6, 16);

        // Act.
        var result = DateLabelService.GetLabel(twoDaysAgo, today, EnglishCulture);

        // Assert.
        new DateLabel("Monday", "16 Jun 2025", TitleIsBold: true, DateIsBold: false)
            .ToExpectedObject()
            .ShouldEqual(result);
    }

    [Fact]
    public void GetLabel_TwoDaysInFuture_ReturnsDayName()
    {
        // Arrange.
        var today = new DateOnly(2025, 6, 18);
        var twoDaysAhead = new DateOnly(2025, 6, 20);

        // Act.
        var result = DateLabelService.GetLabel(twoDaysAhead, today, EnglishCulture);

        // Assert.
        new DateLabel("Friday", "20 Jun 2025", TitleIsBold: true, DateIsBold: false)
            .ToExpectedObject()
            .ShouldEqual(result);
    }
}
