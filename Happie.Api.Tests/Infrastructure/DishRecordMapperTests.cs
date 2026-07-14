using Happie.Api.Domain;
using Happie.Api.Infrastructure.Entities;
using Happie.Api.Infrastructure.Mappers;

namespace Happie.Api.Tests.Infrastructure;

/// <summary>Unit tests for <see cref="DishRecordMapper"/> SavedDishId mapping.</summary>
public class DishRecordMapperTests
{
    private readonly DishRecordMapper _sut = new();

    /// <summary>When SavedDishId on the entity is Guid.Empty, ToModel maps it to null.</summary>
    [Fact]
    public void ToModel_SavedDishIdEmpty_MapsToNull()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var date = new DateOnly(2025, 7, 15);
        var entity = new DishRecordEntity(householdId, date)
        {
            Description = "Test",
            SavedDishId = Guid.Empty,
        };

        // Act.
        var result = _sut.ToModel(householdId, date, entity);

        // Assert.
        Assert.Null(result.SavedDishId);
    }

    /// <summary>When SavedDishId on the entity is a non-empty Guid, ToModel maps it to that Guid.</summary>
    [Fact]
    public void ToModel_SavedDishIdNonEmpty_MapsToGuid()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var date = new DateOnly(2025, 7, 15);
        var savedDishId = Guid.NewGuid();
        var entity = new DishRecordEntity(householdId, date)
        {
            Description = "Test",
            SavedDishId = savedDishId,
        };

        // Act.
        var result = _sut.ToModel(householdId, date, entity);

        // Assert.
        Assert.Equal(savedDishId, result.SavedDishId);
    }

    /// <summary>When SavedDishId on the domain record is null, ToEntity sets Guid.Empty.</summary>
    [Fact]
    public void ToEntity_NullSavedDishId_SetsEmptyGuid()
    {
        // Arrange.
        var record = new DishRecord(
            Guid.NewGuid(),
            new DateOnly(2025, 7, 15),
            "Test",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            null,
            DateTimeOffset.UtcNow,
            null);

        // Act.
        var result = _sut.ToEntity(record);

        // Assert.
        Assert.Equal(Guid.Empty, result.SavedDishId);
    }
}
