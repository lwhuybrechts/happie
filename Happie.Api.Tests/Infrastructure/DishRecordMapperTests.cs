using Happie.Api.Domain;
using Happie.Api.Infrastructure.Entities;
using Happie.Api.Infrastructure.Mappers;

namespace Happie.Api.Tests.Infrastructure;

/// <summary>Unit tests for <see cref="DishRecordMapper"/>.</summary>
public class DishRecordMapperTests
{
    private readonly DishRecordMapper _sut = new();

    /// <summary>ToModel maps entity properties correctly to a DishRecord.</summary>
    [Fact]
    public void ToModel_ValidEntity_MapsCorrectly()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var date = new DateOnly(2025, 7, 15);
        var lastChangedBy = Guid.NewGuid();
        var entity = new DishRecordEntity(householdId, date)
        {
            Description = "Test",
            LastChangedByHousemateId = lastChangedBy,
            LastChangedAt = new DateTimeOffset(2025, 7, 15, 12, 0, 0, TimeSpan.Zero),
            DinnerTimeHour = 18,
            DinnerTimeMinute = 30,
            LastModified = new DateTimeOffset(2025, 7, 15, 12, 0, 0, TimeSpan.Zero),
        };

        // Act.
        var result = _sut.ToModel(householdId, date, entity);

        // Assert.
        Assert.Equal("Test", result.Description);
        Assert.Equal(lastChangedBy, result.LastChangedByHousemateId);
        Assert.Equal(new TimeOnly(18, 30), result.DinnerTime);
    }

    /// <summary>ToEntity maps a DishRecord to entity correctly.</summary>
    [Fact]
    public void ToEntity_ValidRecord_MapsCorrectly()
    {
        // Arrange.
        var record = new DishRecord(
            Guid.NewGuid(),
            new DateOnly(2025, 7, 15),
            "Test",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            new TimeOnly(18, 30),
            DateTimeOffset.UtcNow);

        // Act.
        var result = _sut.ToEntity(record);

        // Assert.
        Assert.Equal("Test", result.Description);
        Assert.Equal(18, result.DinnerTimeHour);
        Assert.Equal(30, result.DinnerTimeMinute);
    }
}
