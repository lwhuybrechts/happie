using Azure.Data.Tables;
using Happie.Api.Infrastructure;
using Happie.Api.Infrastructure.Entities;

namespace Happie.Api.IntegrationTests.Infrastructure;

/// <summary>Integration tests for <see cref="TableStorageClient.QueryByRowKeyRangeAsync{T}"/> against Azurite.</summary>
public class TableStorageClientRowKeyRangeIntegrationTests
{
    private const string TableName = "RowKeyRangeTests";

    private readonly TableStorageClient _sut;
    private readonly TableServiceClient _tableServiceClient;

    public TableStorageClientRowKeyRangeIntegrationTests()
    {
        var connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING")
            ?? "UseDevelopmentStorage=true";

        _tableServiceClient = new TableServiceClient(connectionString);

        TableHelper.TruncateTable(_tableServiceClient, TableName);

        _sut = new TableStorageClient(_tableServiceClient);
    }

    [Fact]
    public async Task QueryByRowKeyRangeAsync_KnownEntities_ReturnsOnlyEntitiesWithinRange()
    {
        // Arrange.
        var partitionKey = Guid.NewGuid().ToString();

        await InsertEntity(partitionKey, "2025-03-10_aaa");
        await InsertEntity(partitionKey, "2025-03-15_bbb");
        await InsertEntity(partitionKey, "2025-03-20_ccc");
        await InsertEntity(partitionKey, "2025-03-25_ddd");

        // Act.
        var results = await _sut.QueryByRowKeyRangeAsync<DayPlanDishLinkEntity>(
            TableName, partitionKey, "2025-03-15", "2025-03-25");

        // Assert.
        Assert.Equal(2, results.Count);
        Assert.Equal("2025-03-15_bbb", results[0].RowKey);
        Assert.Equal("2025-03-20_ccc", results[1].RowKey);
    }

    [Fact]
    public async Task QueryByRowKeyRangeAsync_NoEntitiesMatch_ReturnsEmptyList()
    {
        // Arrange.
        var partitionKey = Guid.NewGuid().ToString();

        await InsertEntity(partitionKey, "2025-01-01_aaa");
        await InsertEntity(partitionKey, "2025-01-02_bbb");

        // Act.
        var results = await _sut.QueryByRowKeyRangeAsync<DayPlanDishLinkEntity>(
            TableName, partitionKey, "2025-06-01", "2025-06-30");

        // Assert.
        Assert.Empty(results);
    }

    [Fact]
    public async Task QueryByRowKeyRangeAsync_StartGreaterThanOrEqualToEnd_ReturnsEmptyList()
    {
        // Arrange.
        var partitionKey = Guid.NewGuid().ToString();

        await InsertEntity(partitionKey, "2025-03-15_aaa");

        // Act — start equals end.
        var resultsEqual = await _sut.QueryByRowKeyRangeAsync<DayPlanDishLinkEntity>(
            TableName, partitionKey, "2025-03-15", "2025-03-15");

        // Act — start greater than end.
        var resultsGreater = await _sut.QueryByRowKeyRangeAsync<DayPlanDishLinkEntity>(
            TableName, partitionKey, "2025-03-20", "2025-03-10");

        // Assert.
        Assert.Empty(resultsEqual);
        Assert.Empty(resultsGreater);
    }

    [Fact]
    public async Task QueryByRowKeyRangeAsync_Results_AreOrderedByRowKeyAscending()
    {
        // Arrange.
        var partitionKey = Guid.NewGuid().ToString();

        // Insert in non-alphabetical order.
        await InsertEntity(partitionKey, "c-entity");
        await InsertEntity(partitionKey, "a-entity");
        await InsertEntity(partitionKey, "d-entity");
        await InsertEntity(partitionKey, "b-entity");

        // Act.
        var results = await _sut.QueryByRowKeyRangeAsync<DayPlanDishLinkEntity>(
            TableName, partitionKey, "a", "e");

        // Assert.
        Assert.Equal(4, results.Count);
        Assert.Equal("a-entity", results[0].RowKey);
        Assert.Equal("b-entity", results[1].RowKey);
        Assert.Equal("c-entity", results[2].RowKey);
        Assert.Equal("d-entity", results[3].RowKey);
    }

    private async Task InsertEntity(string partitionKey, string rowKey)
    {
        var tableClient = _tableServiceClient.GetTableClient(TableName);
        await tableClient.CreateIfNotExistsAsync();
        var entity = new DayPlanDishLinkEntity
        {
            PartitionKey = partitionKey,
            RowKey = rowKey,
            SortOrder = 0
        };
        await tableClient.UpsertEntityAsync(entity);
    }
}
