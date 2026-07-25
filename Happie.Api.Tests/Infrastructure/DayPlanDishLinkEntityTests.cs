using Happie.Api.Infrastructure.Entities;

namespace Happie.Api.Tests.Infrastructure;

/// <summary>Unit tests for <see cref="DayPlanDishLinkEntity"/>.</summary>
public class DayPlanDishLinkEntityTests
{
    /// <summary>Parameterized constructor sets PartitionKey to the household GUID string.</summary>
    [Fact]
    public void Constructor_WithKnownValues_SetsPartitionKeyToHouseholdId()
    {
        // Arrange.
        var householdId = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
        var date = new DateOnly(2025, 3, 15);
        var savedDishId = Guid.Parse("11111111-2222-3333-4444-555555555555");

        // Act.
        var entity = new DayPlanDishLinkEntity(householdId, date, savedDishId);

        // Assert.
        Assert.Equal("a1b2c3d4-e5f6-7890-abcd-ef1234567890", entity.PartitionKey);
    }

    /// <summary>Parameterized constructor sets RowKey to date underscore savedDishId format.</summary>
    [Fact]
    public void Constructor_WithKnownValues_SetsRowKeyToDateAndSavedDishId()
    {
        // Arrange.
        var householdId = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
        var date = new DateOnly(2025, 3, 15);
        var savedDishId = Guid.Parse("11111111-2222-3333-4444-555555555555");

        // Act.
        var entity = new DayPlanDishLinkEntity(householdId, date, savedDishId);

        // Assert.
        Assert.Equal("2025-03-15_11111111-2222-3333-4444-555555555555", entity.RowKey);
    }

    /// <summary>Parameterless constructor creates a valid non-null instance.</summary>
    [Fact]
    public void ParameterlessConstructor_CreatesValidInstance()
    {
        // Act.
        var entity = new DayPlanDishLinkEntity();

        // Assert.
        Assert.NotNull(entity);
    }

    /// <summary>Parameterless constructor allows PartitionKey and RowKey to be set manually.</summary>
    [Fact]
    public void ParameterlessConstructor_AllowsSettingKeys()
    {
        // Arrange.
        var entity = new DayPlanDishLinkEntity();

        // Act.
        entity.PartitionKey = "some-partition-key";
        entity.RowKey = "some-row-key";

        // Assert.
        Assert.Equal("some-partition-key", entity.PartitionKey);
        Assert.Equal("some-row-key", entity.RowKey);
    }
}
