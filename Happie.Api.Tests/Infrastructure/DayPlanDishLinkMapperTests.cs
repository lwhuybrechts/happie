using Happie.Api.Domain;
using Happie.Api.Infrastructure.Entities;
using Happie.Api.Infrastructure.Mappers;

namespace Happie.Api.Tests.Infrastructure;

/// <summary>Unit tests for <see cref="DayPlanDishLinkMapper"/>.</summary>
public class DayPlanDishLinkMapperTests
{
    private readonly DayPlanDishLinkMapper _sut = new();

    /// <summary>ToModel correctly parses a leap year date from RowKey.</summary>
    [Fact]
    public void ToModel_LeapYearDate_ParsesCorrectly()
    {
        // Arrange.
        var householdId = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
        var savedDishId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var entity = new DayPlanDishLinkEntity(householdId, new DateOnly(2024, 2, 29), savedDishId)
        {
            SortOrder = 0
        };

        // Act.
        var result = _sut.ToModel(entity);

        // Assert.
        Assert.Equal(householdId, result.HouseholdId);
        Assert.Equal(new DateOnly(2024, 2, 29), result.Date);
        Assert.Equal(savedDishId, result.SavedDishId);
        Assert.Equal(0, result.SortOrder);
    }

    /// <summary>ToModel correctly parses a year boundary date from RowKey.</summary>
    [Fact]
    public void ToModel_YearBoundaryDate_ParsesCorrectly()
    {
        // Arrange.
        var householdId = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
        var savedDishId = Guid.Parse("22222222-3333-4444-5555-666666666666");
        var entity = new DayPlanDishLinkEntity(householdId, new DateOnly(2025, 1, 1), savedDishId)
        {
            SortOrder = 3
        };

        // Act.
        var result = _sut.ToModel(entity);

        // Assert.
        Assert.Equal(householdId, result.HouseholdId);
        Assert.Equal(new DateOnly(2025, 1, 1), result.Date);
        Assert.Equal(savedDishId, result.SavedDishId);
        Assert.Equal(3, result.SortOrder);
    }

    /// <summary>ToModel correctly parses an end-of-year date from RowKey.</summary>
    [Fact]
    public void ToModel_EndOfYearDate_ParsesCorrectly()
    {
        // Arrange.
        var householdId = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
        var savedDishId = Guid.Parse("33333333-4444-5555-6666-777777777777");
        var entity = new DayPlanDishLinkEntity(householdId, new DateOnly(2025, 12, 31), savedDishId)
        {
            SortOrder = 7
        };

        // Act.
        var result = _sut.ToModel(entity);

        // Assert.
        Assert.Equal(householdId, result.HouseholdId);
        Assert.Equal(new DateOnly(2025, 12, 31), result.Date);
        Assert.Equal(savedDishId, result.SavedDishId);
        Assert.Equal(7, result.SortOrder);
    }

    /// <summary>ToEntity produces correct PartitionKey and RowKey from known values.</summary>
    [Fact]
    public void ToEntity_WithKnownValues_ProducesCorrectKeys()
    {
        // Arrange.
        var householdId = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
        var savedDishId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var link = new DayPlanDishLink(householdId, new DateOnly(2025, 3, 15), savedDishId, 2);

        // Act.
        var result = _sut.ToEntity(link);

        // Assert.
        Assert.Equal("a1b2c3d4-e5f6-7890-abcd-ef1234567890", result.PartitionKey);
        Assert.Equal("2025-03-15_11111111-2222-3333-4444-555555555555", result.RowKey);
        Assert.Equal(2, result.SortOrder);
    }
}
