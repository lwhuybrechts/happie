using Azure.Data.Tables;
using Happie.Api.Domain;
using Happie.Api.Infrastructure;
using Happie.Api.Infrastructure.Mappers;
using Happie.Api.Infrastructure.Repositories;
using Happie.Api.IntegrationTests.Infrastructure;

namespace Happie.Api.IntegrationTests.Days;

/// <summary>Integration tests for DayPlanDishLinkRepository against Azurite.</summary>
public class DayPlanDishLinkRepositoryIntegrationTests
{
    private readonly IDayPlanDishLinkRepository _sut;

    public DayPlanDishLinkRepositoryIntegrationTests()
    {
        var connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING")
            ?? "UseDevelopmentStorage=true";

        var tableServiceClient = new TableServiceClient(connectionString);

        TableHelper.TruncateTable(tableServiceClient, "DayPlanDishLinks");

        var storageClient = new TableStorageClient(tableServiceClient);
        _sut = new DayPlanDishLinkRepository(storageClient, new DayPlanDishLinkMapper());
    }

    [Fact]
    public async Task GetByDateAsync_MultipleLinksForDate_ReturnsSortedBySortOrder()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var date = new DateOnly(2025, 6, 15);
        var dishIdA = Guid.NewGuid();
        var dishIdB = Guid.NewGuid();
        var dishIdC = Guid.NewGuid();

        await _sut.CreateAsync(new DayPlanDishLink(householdId, date, dishIdA, 2));
        await _sut.CreateAsync(new DayPlanDishLink(householdId, date, dishIdB, 0));
        await _sut.CreateAsync(new DayPlanDishLink(householdId, date, dishIdC, 1));

        // Act.
        var result = await _sut.GetByDateAsync(householdId, date);

        // Assert.
        Assert.Equal(3, result.Count);
        Assert.Equal(dishIdB, result[0].SavedDishId);
        Assert.Equal(dishIdC, result[1].SavedDishId);
        Assert.Equal(dishIdA, result[2].SavedDishId);
    }

    [Fact]
    public async Task GetByDateAsync_NoLinksForDate_ReturnsEmptyList()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var date = new DateOnly(2025, 6, 16);

        // Act.
        var result = await _sut.GetByDateAsync(householdId, date);

        // Assert.
        Assert.Empty(result);
    }

    [Fact]
    public async Task ReplaceAllAsync_DeletesExistingAndInsertsNewLinks()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var date = new DateOnly(2025, 7, 1);
        var originalDishId = Guid.NewGuid();
        var newDishIdA = Guid.NewGuid();
        var newDishIdB = Guid.NewGuid();

        await _sut.CreateAsync(new DayPlanDishLink(householdId, date, originalDishId, 0));

        var newLinks = new List<DayPlanDishLink>
        {
            new(householdId, date, newDishIdA, 0),
            new(householdId, date, newDishIdB, 1),
        };

        // Act.
        await _sut.ReplaceAllAsync(householdId, date, newLinks);
        var result = await _sut.GetByDateAsync(householdId, date);

        // Assert.
        Assert.Equal(2, result.Count);
        Assert.Equal(newDishIdA, result[0].SavedDishId);
        Assert.Equal(newDishIdB, result[1].SavedDishId);
    }

    [Fact]
    public async Task ReplaceAllAsync_EmptyList_DeletesAllForDate()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var date = new DateOnly(2025, 7, 2);
        var dishIdA = Guid.NewGuid();
        var dishIdB = Guid.NewGuid();

        await _sut.CreateAsync(new DayPlanDishLink(householdId, date, dishIdA, 0));
        await _sut.CreateAsync(new DayPlanDishLink(householdId, date, dishIdB, 1));

        // Act.
        await _sut.ReplaceAllAsync(householdId, date, new List<DayPlanDishLink>());
        var result = await _sut.GetByDateAsync(householdId, date);

        // Assert.
        Assert.Empty(result);
    }

    [Fact]
    public async Task DeleteAllAsync_RemovesAllEntitiesForDate()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var date = new DateOnly(2025, 7, 3);
        var otherDate = new DateOnly(2025, 7, 4);
        var dishIdA = Guid.NewGuid();
        var dishIdB = Guid.NewGuid();
        var dishIdOther = Guid.NewGuid();

        await _sut.CreateAsync(new DayPlanDishLink(householdId, date, dishIdA, 0));
        await _sut.CreateAsync(new DayPlanDishLink(householdId, date, dishIdB, 1));
        await _sut.CreateAsync(new DayPlanDishLink(householdId, otherDate, dishIdOther, 0));

        // Act.
        await _sut.DeleteAllAsync(householdId, date);
        var deletedResult = await _sut.GetByDateAsync(householdId, date);
        var otherResult = await _sut.GetByDateAsync(householdId, otherDate);

        // Assert.
        Assert.Empty(deletedResult);
        Assert.Single(otherResult);
        Assert.Equal(dishIdOther, otherResult[0].SavedDishId);
    }

    [Fact]
    public async Task GetAllByHouseholdAsync_ReturnsAllLinksFromSinglePartition()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var dateA = new DateOnly(2025, 8, 1);
        var dateB = new DateOnly(2025, 8, 2);
        var dishIdA = Guid.NewGuid();
        var dishIdB = Guid.NewGuid();
        var dishIdC = Guid.NewGuid();

        await _sut.CreateAsync(new DayPlanDishLink(householdId, dateA, dishIdA, 0));
        await _sut.CreateAsync(new DayPlanDishLink(householdId, dateA, dishIdB, 1));
        await _sut.CreateAsync(new DayPlanDishLink(householdId, dateB, dishIdC, 0));

        // Act.
        var result = await _sut.GetAllByHouseholdAsync(householdId);

        // Assert.
        Assert.Equal(3, result.Count);
        Assert.Contains(result, x => x.SavedDishId == dishIdA);
        Assert.Contains(result, x => x.SavedDishId == dishIdB);
        Assert.Contains(result, x => x.SavedDishId == dishIdC);
    }

    [Fact]
    public async Task GetAllByHouseholdAsync_DoesNotReturnOtherHouseholds()
    {
        // Arrange.
        var householdIdA = Guid.NewGuid();
        var householdIdB = Guid.NewGuid();
        var date = new DateOnly(2025, 8, 3);
        var dishIdA = Guid.NewGuid();
        var dishIdB = Guid.NewGuid();

        await _sut.CreateAsync(new DayPlanDishLink(householdIdA, date, dishIdA, 0));
        await _sut.CreateAsync(new DayPlanDishLink(householdIdB, date, dishIdB, 0));

        // Act.
        var result = await _sut.GetAllByHouseholdAsync(householdIdA);

        // Assert.
        Assert.Single(result);
        Assert.Equal(dishIdA, result[0].SavedDishId);
    }

    [Fact]
    public async Task CreateAsync_UpsertsWithCorrectKeyFormat()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var date = new DateOnly(2025, 9, 10);
        var savedDishId = Guid.NewGuid();
        var link = new DayPlanDishLink(householdId, date, savedDishId, 3);

        // Act.
        await _sut.CreateAsync(link);
        var result = await _sut.GetByDateAsync(householdId, date);

        // Assert.
        Assert.Single(result);
        Assert.Equal(householdId, result[0].HouseholdId);
        Assert.Equal(date, result[0].Date);
        Assert.Equal(savedDishId, result[0].SavedDishId);
        Assert.Equal(3, result[0].SortOrder);
    }

    [Fact]
    public async Task CreateAsync_UpsertOverwritesExistingEntity()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var date = new DateOnly(2025, 9, 11);
        var savedDishId = Guid.NewGuid();

        await _sut.CreateAsync(new DayPlanDishLink(householdId, date, savedDishId, 0));

        // Act.
        await _sut.CreateAsync(new DayPlanDishLink(householdId, date, savedDishId, 5));
        var result = await _sut.GetByDateAsync(householdId, date);

        // Assert.
        Assert.Single(result);
        Assert.Equal(5, result[0].SortOrder);
    }
}
