using Azure.Data.Tables;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Api.Infrastructure;

namespace Happie.Api.IntegrationTests.Infrastructure;

// Feature: dayplan-dishlink-repartition, Property 2: RowKey range query returns exactly the entities within range
/// <summary>
/// Property-based tests verifying that <see cref="TableStorageClient.QueryByRowKeyRangeAsync{T}"/>
/// returns exactly the entities whose RowKey is within the specified range.
/// </summary>
public class TableStorageClientRowKeyRangePropertyTests
{
    private readonly TableServiceClient _tableServiceClient;
    private readonly ITableStorageClient _sut;
    private readonly string _tableName;

    public TableStorageClientRowKeyRangePropertyTests()
    {
        var connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING")
            ?? "UseDevelopmentStorage=true";

        _tableServiceClient = new TableServiceClient(connectionString);

        // Use a unique table name per test class instance to avoid conflicts.
        _tableName = $"RKRange{Guid.NewGuid():N}";
        var tableClient = _tableServiceClient.GetTableClient(_tableName);
        tableClient.CreateIfNotExists();

        _sut = new TableStorageClient(_tableServiceClient);
    }

    /// <summary>
    /// For any set of entities in a single partition with arbitrary RowKey values, and for any pair
    /// of strings (rowKeyStart, rowKeyEnd), the QueryByRowKeyRangeAsync method SHALL return exactly
    /// those entities whose RowKey is lexicographically >= rowKeyStart and &lt; rowKeyEnd. When
    /// rowKeyStart >= rowKeyEnd, the result SHALL be an empty list.
    /// Validates: Requirements 8.1, 8.2, 8.3, 8.5
    /// </summary>
    [Property(MaxTest = 100)]
    public Property QueryByRowKeyRangeAsync_ReturnsExactlyEntitiesWithinRange()
    {
        return Prop.ForAll(
            RowKeyRangeScenarioArb(),
            async scenario =>
            {
                var partitionKey = scenario.PartitionKey;

                // Arrange: insert all entities into the table.
                var tableClient = _tableServiceClient.GetTableClient(_tableName);
                foreach (var entity in scenario.Entities)
                {
                    entity.PartitionKey = partitionKey;
                    await tableClient.UpsertEntityAsync(entity);
                }

                // Act.
                var result = await _sut.QueryByRowKeyRangeAsync<TestEntity>(
                    _tableName, partitionKey, scenario.RowKeyStart, scenario.RowKeyEnd);

                // Compute expected result.
                var expected = ComputeExpectedResult(scenario);

                // Assert.
                var resultRowKeys = result.Select(x => x.RowKey).OrderBy(x => x, StringComparer.Ordinal).ToList();
                var expectedRowKeys = expected.OrderBy(x => x, StringComparer.Ordinal).ToList();

                // Clean up inserted entities to avoid cross-iteration interference.
                foreach (var entity in scenario.Entities)
                {
                    await tableClient.DeleteEntityAsync(partitionKey, entity.RowKey);
                }

                var countMatches = (resultRowKeys.Count == expectedRowKeys.Count)
                    .Label($"Count mismatch: expected {expectedRowKeys.Count} but got {resultRowKeys.Count}. " +
                           $"Range: [{scenario.RowKeyStart}, {scenario.RowKeyEnd}). " +
                           $"Expected keys: [{string.Join(", ", expectedRowKeys)}]. " +
                           $"Actual keys: [{string.Join(", ", resultRowKeys)}]");

                var contentMatches = resultRowKeys.SequenceEqual(expectedRowKeys)
                    .Label($"Content mismatch. " +
                           $"Range: [{scenario.RowKeyStart}, {scenario.RowKeyEnd}). " +
                           $"Expected keys: [{string.Join(", ", expectedRowKeys)}]. " +
                           $"Actual keys: [{string.Join(", ", resultRowKeys)}]");

                return countMatches.And(contentMatches);
            });
    }

    private static List<string> ComputeExpectedResult(RowKeyRangeScenario scenario)
    {
        // When rowKeyStart >= rowKeyEnd, expect an empty list.
        if (string.Compare(scenario.RowKeyStart, scenario.RowKeyEnd, StringComparison.Ordinal) >= 0)
            return [];

        return scenario.Entities
            .Select(x => x.RowKey)
            .Where(x => string.Compare(x, scenario.RowKeyStart, StringComparison.Ordinal) >= 0
                     && string.Compare(x, scenario.RowKeyEnd, StringComparison.Ordinal) < 0)
            .ToList();
    }

    private static Arbitrary<RowKeyRangeScenario> RowKeyRangeScenarioArb()
    {
        var alphanumericCharGen = Gen.Elements(
            'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j',
            'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't',
            'u', 'v', 'w', 'x', 'y', 'z', 'A', 'B', 'C', 'D',
            'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N',
            'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X',
            'Y', 'Z', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9');

        // Generate a random alphanumeric string of length 10–50.
        var rowKeyStringGen = Gen.Choose(10, 50)
            .SelectMany(length => Gen.ListOf(alphanumericCharGen, length)
                .Select(chars => new string(chars.ToArray())));

        // Generate a partition key (unique per scenario to avoid cross-iteration collisions).
        var partitionKeyGen = ArbMap.Default.GeneratorFor<Guid>()
            .Select(x => x.ToString());

        // Generate 0–20 entities with random RowKey strings.
        var entitiesGen = Gen.Choose(0, 20)
            .SelectMany(count => Gen.ListOf(rowKeyStringGen, count)
                .Select(x => x.Select(rowKey => new TestEntity { RowKey = rowKey }).ToList()));

        // Generate random rowKeyStart and rowKeyEnd strings.
        var rangeStringGen = rowKeyStringGen;

        var scenarioGen = partitionKeyGen.SelectMany(partitionKey =>
            entitiesGen.SelectMany(entities =>
                rangeStringGen.SelectMany(rowKeyStart =>
                    rangeStringGen.Select(rowKeyEnd =>
                        new RowKeyRangeScenario(partitionKey, entities, rowKeyStart, rowKeyEnd)))));

        return Arb.From(scenarioGen);
    }
}

/// <summary>A simple entity used for property testing RowKey range queries.</summary>
public class TestEntity : MyTableEntity
{
    public TestEntity() { }
}

/// <summary>Represents a single test scenario for RowKey range query property tests.</summary>
public record RowKeyRangeScenario(
    string PartitionKey,
    List<TestEntity> Entities,
    string RowKeyStart,
    string RowKeyEnd);
