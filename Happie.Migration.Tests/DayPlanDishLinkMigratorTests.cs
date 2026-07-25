using Happie.Migration;

namespace Happie.Migration.Tests;

/// <summary>Unit tests for <see cref="DayPlanDishLinkMigrator"/>.</summary>
public class DayPlanDishLinkMigratorTests
{
    /// <summary>Valid old-format PartitionKey with lowercase GUID and date is detected.</summary>
    [Fact]
    public void IsOldFormat_ValidLowercaseGuidWithDate_ReturnsTrue()
    {
        // Arrange.
        var partitionKey = "a1b2c3d4-e5f6-7890-abcd-ef1234567890_2025-03-15";

        // Act.
        var result = DayPlanDishLinkMigrator.IsOldFormat(partitionKey);

        // Assert.
        Assert.True(result);
    }

    /// <summary>Valid old-format PartitionKey with uppercase GUID is detected.</summary>
    [Fact]
    public void IsOldFormat_ValidUppercaseGuidWithDate_ReturnsTrue()
    {
        // Arrange.
        var partitionKey = "A1B2C3D4-E5F6-7890-ABCD-EF1234567890_2025-03-15";

        // Act.
        var result = DayPlanDishLinkMigrator.IsOldFormat(partitionKey);

        // Assert.
        Assert.True(result);
    }

    /// <summary>PartitionKey with just a GUID (new format) is not detected as old format.</summary>
    [Fact]
    public void IsOldFormat_GuidOnly_ReturnsFalse()
    {
        // Arrange.
        var partitionKey = "a1b2c3d4-e5f6-7890-abcd-ef1234567890";

        // Act.
        var result = DayPlanDishLinkMigrator.IsOldFormat(partitionKey);

        // Assert.
        Assert.False(result);
    }

    /// <summary>PartitionKey with a non-GUID string is not detected as old format.</summary>
    [Fact]
    public void IsOldFormat_HouseholdsString_ReturnsFalse()
    {
        // Arrange.
        var partitionKey = "households";

        // Act.
        var result = DayPlanDishLinkMigrator.IsOldFormat(partitionKey);

        // Assert.
        Assert.False(result);
    }

    /// <summary>Random string without GUID pattern is not detected as old format.</summary>
    [Fact]
    public void IsOldFormat_RandomString_ReturnsFalse()
    {
        // Arrange.
        var partitionKey = "random_string";

        // Act.
        var result = DayPlanDishLinkMigrator.IsOldFormat(partitionKey);

        // Assert.
        Assert.False(result);
    }

    /// <summary>GUID with invalid date suffix is not detected as old format.</summary>
    [Fact]
    public void IsOldFormat_GuidWithInvalidDate_ReturnsFalse()
    {
        // Arrange.
        var partitionKey = "a1b2c3d4-e5f6-7890-abcd-ef1234567890_invalid-date";

        // Act.
        var result = DayPlanDishLinkMigrator.IsOldFormat(partitionKey);

        // Assert.
        Assert.False(result);
    }

    /// <summary>Empty string is not detected as old format.</summary>
    [Fact]
    public void IsOldFormat_EmptyString_ReturnsFalse()
    {
        // Arrange.
        var partitionKey = "";

        // Act.
        var result = DayPlanDishLinkMigrator.IsOldFormat(partitionKey);

        // Assert.
        Assert.False(result);
    }

    /// <summary>GUID with extra text after the date is not detected as old format.</summary>
    [Fact]
    public void IsOldFormat_GuidWithDateAndExtraText_ReturnsFalse()
    {
        // Arrange.
        var partitionKey = "a1b2c3d4-e5f6-7890-abcd-ef1234567890_2025-03-15_extra";

        // Act.
        var result = DayPlanDishLinkMigrator.IsOldFormat(partitionKey);

        // Assert.
        Assert.False(result);
    }

    /// <summary>ProcessRecordAsync skips creation when new-format record already exists.</summary>
    [Fact]
    public async Task ProcessRecordAsync_TargetAlreadyExists_SkipsAndDeletesOld()
    {
        // Arrange.
        var result = new MigrationResult();
        var partitionKey = "a1b2c3d4-e5f6-7890-abcd-ef1234567890_2025-03-15";
        var rowKey = "11111111-2222-3333-4444-555555555555";
        var deleteWasCalled = false;
        var createWasCalled = false;

        // Act.
        await DayPlanDishLinkMigrator.ProcessRecordAsync(
            partitionKey,
            rowKey,
            sortOrder: 2,
            existsAsync: (_, _) => Task.FromResult(true),
            createAsync: (_, _, _) =>
            {
                createWasCalled = true;
                return Task.CompletedTask;
            },
            deleteAsync: (_, _) =>
            {
                deleteWasCalled = true;
                return Task.CompletedTask;
            },
            result);

        // Assert.
        Assert.Equal(0, result.Migrated);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(0, result.Failed);
        Assert.False(createWasCalled);
        Assert.True(deleteWasCalled);
    }

    /// <summary>ProcessRecordAsync creates new record and deletes old when target does not exist.</summary>
    [Fact]
    public async Task ProcessRecordAsync_TargetDoesNotExist_CreatesAndDeletesOld()
    {
        // Arrange.
        var result = new MigrationResult();
        var partitionKey = "a1b2c3d4-e5f6-7890-abcd-ef1234567890_2025-03-15";
        var rowKey = "11111111-2222-3333-4444-555555555555";
        string? createdPartitionKey = null;
        string? createdRowKey = null;
        int? createdSortOrder = null;

        // Act.
        await DayPlanDishLinkMigrator.ProcessRecordAsync(
            partitionKey,
            rowKey,
            sortOrder: 5,
            existsAsync: (_, _) => Task.FromResult(false),
            createAsync: (partitionKey, rowKey, sortOrder) =>
            {
                createdPartitionKey = partitionKey;
                createdRowKey = rowKey;
                createdSortOrder = sortOrder;
                return Task.CompletedTask;
            },
            deleteAsync: (_, _) => Task.CompletedTask,
            result);

        // Assert.
        Assert.Equal(1, result.Migrated);
        Assert.Equal(0, result.Skipped);
        Assert.Equal(0, result.Failed);
        Assert.Equal("a1b2c3d4-e5f6-7890-abcd-ef1234567890", createdPartitionKey);
        Assert.Equal("2025-03-15_11111111-2222-3333-4444-555555555555", createdRowKey);
        Assert.Equal(5, createdSortOrder);
    }

    /// <summary>ProcessRecordAsync increments failed count when an exception is thrown.</summary>
    [Fact]
    public async Task ProcessRecordAsync_ExceptionDuringCreate_IncrementsFailedCount()
    {
        // Arrange.
        var result = new MigrationResult();
        var partitionKey = "a1b2c3d4-e5f6-7890-abcd-ef1234567890_2025-03-15";
        var rowKey = "11111111-2222-3333-4444-555555555555";

        // Act.
        await DayPlanDishLinkMigrator.ProcessRecordAsync(
            partitionKey,
            rowKey,
            sortOrder: 0,
            existsAsync: (_, _) => Task.FromResult(false),
            createAsync: (_, _, _) => throw new InvalidOperationException("Storage error"),
            deleteAsync: (_, _) => Task.CompletedTask,
            result);

        // Assert.
        Assert.Equal(0, result.Migrated);
        Assert.Equal(0, result.Skipped);
        Assert.Equal(1, result.Failed);
    }

    /// <summary>ProcessRecordAsync increments failed count when exists check throws.</summary>
    [Fact]
    public async Task ProcessRecordAsync_ExceptionDuringExistsCheck_IncrementsFailedCount()
    {
        // Arrange.
        var result = new MigrationResult();
        var partitionKey = "a1b2c3d4-e5f6-7890-abcd-ef1234567890_2025-03-15";
        var rowKey = "11111111-2222-3333-4444-555555555555";

        // Act.
        await DayPlanDishLinkMigrator.ProcessRecordAsync(
            partitionKey,
            rowKey,
            sortOrder: 0,
            existsAsync: (_, _) => throw new InvalidOperationException("Connection failed"),
            createAsync: (_, _, _) => Task.CompletedTask,
            deleteAsync: (_, _) => Task.CompletedTask,
            result);

        // Assert.
        Assert.Equal(0, result.Migrated);
        Assert.Equal(0, result.Skipped);
        Assert.Equal(1, result.Failed);
    }

    /// <summary>ProcessRecordAsync accumulates totals across multiple calls.</summary>
    [Fact]
    public async Task ProcessRecordAsync_MultipleCalls_AccumulatesTotals()
    {
        // Arrange.
        var result = new MigrationResult();
        var rowKey = "11111111-2222-3333-4444-555555555555";

        // Act - one migrated.
        await DayPlanDishLinkMigrator.ProcessRecordAsync(
            "a1b2c3d4-e5f6-7890-abcd-ef1234567890_2025-03-15",
            rowKey,
            sortOrder: 0,
            existsAsync: (_, _) => Task.FromResult(false),
            createAsync: (_, _, _) => Task.CompletedTask,
            deleteAsync: (_, _) => Task.CompletedTask,
            result);

        // Act - one skipped.
        await DayPlanDishLinkMigrator.ProcessRecordAsync(
            "b2c3d4e5-f6a7-8901-bcde-f12345678901_2025-03-16",
            rowKey,
            sortOrder: 1,
            existsAsync: (_, _) => Task.FromResult(true),
            createAsync: (_, _, _) => Task.CompletedTask,
            deleteAsync: (_, _) => Task.CompletedTask,
            result);

        // Act - one failed.
        await DayPlanDishLinkMigrator.ProcessRecordAsync(
            "c3d4e5f6-a7b8-9012-cdef-123456789012_2025-03-17",
            rowKey,
            sortOrder: 2,
            existsAsync: (_, _) => throw new InvalidOperationException("error"),
            createAsync: (_, _, _) => Task.CompletedTask,
            deleteAsync: (_, _) => Task.CompletedTask,
            result);

        // Assert.
        Assert.Equal(1, result.Migrated);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(1, result.Failed);
    }

    /// <summary>ProcessRecordAsync does nothing for non-old-format PartitionKeys.</summary>
    [Fact]
    public async Task ProcessRecordAsync_NewFormatPartitionKey_DoesNothing()
    {
        // Arrange.
        var result = new MigrationResult();
        var partitionKey = "a1b2c3d4-e5f6-7890-abcd-ef1234567890";
        var rowKey = "2025-03-15_11111111-2222-3333-4444-555555555555";
        var anyCallbackCalled = false;

        // Act.
        await DayPlanDishLinkMigrator.ProcessRecordAsync(
            partitionKey,
            rowKey,
            sortOrder: 0,
            existsAsync: (_, _) =>
            {
                anyCallbackCalled = true;
                return Task.FromResult(false);
            },
            createAsync: (_, _, _) =>
            {
                anyCallbackCalled = true;
                return Task.CompletedTask;
            },
            deleteAsync: (_, _) =>
            {
                anyCallbackCalled = true;
                return Task.CompletedTask;
            },
            result);

        // Assert.
        Assert.Equal(0, result.Migrated);
        Assert.Equal(0, result.Skipped);
        Assert.Equal(0, result.Failed);
        Assert.False(anyCallbackCalled);
    }
}
