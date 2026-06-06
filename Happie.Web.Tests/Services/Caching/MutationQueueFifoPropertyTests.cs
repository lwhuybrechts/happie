using System.Text.Json;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Web.Services.Caching;
using Microsoft.JSInterop;
using Moq;

namespace Happie.Web.Tests.Services.Caching;

// Feature: offline-cache, Property 9: Mutation queue preserves data and FIFO order
public class MutationQueueFifoPropertyTests
{
    private static readonly string[] Methods = ["PUT", "DELETE"];
    private static readonly string[] MutationTypes = ["attendance", "dish", "comment"];

    private static readonly Arbitrary<List<QueuedMutation>> MutationListArb =
        CreateMutationGen()
            .ListOf()
            .Select(x => x.ToList())
            .Where(x => x.Count > 0 && x.Count <= 20)
            .ToArbitrary();

    // Feature: offline-cache, Property 9: Mutation queue preserves data and FIFO order
    // Validates: Requirements 6.1, 6.8, 6.9
    [Property(MaxTest = 100)]
    public Property EnqueueThenDequeue_PreservesDataAndFifoOrder()
    {
        return Prop.ForAll(
            MutationListArb,
            async mutations =>
            {
                // Arrange.
                var householdId = "household-" + Guid.NewGuid().ToString("N")[..8];
                var queue = new Queue<JsonElement>();
                var nextId = 1;
                var jsRuntimeMock = CreateJsRuntimeMock(queue, () => nextId++);
                var sut = new MutationQueue(jsRuntimeMock.Object);
                await sut.InitializeAsync();

                // Act.
                foreach (var mutation in mutations)
                    await sut.EnqueueAsync(householdId, mutation);

                var dequeued = new List<QueuedMutation>();
                for (var i = 0; i < mutations.Count; i++)
                {
                    var result = await sut.DequeueAsync(householdId);
                    if (result is not null)
                        dequeued.Add(result);
                }

                // Assert.
                var countMatches = dequeued.Count == mutations.Count;
                if (!countMatches)
                    return countMatches.Label(
                        $"Expected {mutations.Count} dequeued mutations but got {dequeued.Count}");

                for (var i = 0; i < mutations.Count; i++)
                {
                    var expected = mutations[i];
                    var actual = dequeued[i];

                    var methodMatch = actual.Method == expected.Method;
                    if (!methodMatch)
                        return methodMatch.Label(
                            $"Index {i}: expected Method '{expected.Method}' but got '{actual.Method}'");

                    var urlMatch = actual.Url == expected.Url;
                    if (!urlMatch)
                        return urlMatch.Label(
                            $"Index {i}: expected Url '{expected.Url}' but got '{actual.Url}'");

                    var bodyMatch = actual.Body == expected.Body;
                    if (!bodyMatch)
                        return bodyMatch.Label(
                            $"Index {i}: expected Body '{expected.Body}' but got '{actual.Body}'");

                    // CreatedAt loses sub-millisecond precision (stored as unix ms).
                    var expectedCreatedAtMs = expected.CreatedAt.ToUnixTimeMilliseconds();
                    var actualCreatedAtMs = actual.CreatedAt.ToUnixTimeMilliseconds();
                    var createdAtMatch = actualCreatedAtMs == expectedCreatedAtMs;
                    if (!createdAtMatch)
                        return createdAtMatch.Label(
                            $"Index {i}: expected CreatedAt {expectedCreatedAtMs}ms but got {actualCreatedAtMs}ms");

                    var dateMatch = actual.Date == expected.Date;
                    if (!dateMatch)
                        return dateMatch.Label(
                            $"Index {i}: expected Date '{expected.Date}' but got '{actual.Date}'");

                    var mutationTypeMatch = actual.MutationType == expected.MutationType;
                    if (!mutationTypeMatch)
                        return mutationTypeMatch.Label(
                            $"Index {i}: expected MutationType '{expected.MutationType}' but got '{actual.MutationType}'");

                    var headersMatch = expected.Headers.Count == actual.Headers.Count
                        && expected.Headers.All(x =>
                            actual.Headers.TryGetValue(x.Key, out var value) && value == x.Value);
                    if (!headersMatch)
                        return headersMatch.Label(
                            $"Index {i}: headers mismatch. Expected keys [{string.Join(", ", expected.Headers.Keys)}] " +
                            $"but got [{string.Join(", ", actual.Headers.Keys)}]");
                }

                return true.Label("All mutations preserved in FIFO order");
            });
    }

    private static Gen<QueuedMutation> CreateMutationGen()
    {
        var methodGen = Gen.Elements(Methods);
        var urlGen = Gen.Elements(
            "/api/days/2024-01-15/attendance/abc",
            "/api/days/2024-06-01/dish",
            "/api/days/2024-12-31/comments/xyz");
        var bodyGen = Gen.OneOf(
            Gen.Constant<string?>(null),
            Gen.Elements<string?>("{\"status\":\"EatingIn\"}", "{\"description\":\"Pasta\"}", "{\"text\":\"Yum\"}"));
        var mutationTypeGen = Gen.Elements(MutationTypes);
        // Generate unix seconds in a reasonable range (2020–2025), then convert to milliseconds.
        var createdAtGen = Gen.Choose(1_577_836_800, 1_735_689_600)
            .Select(x => DateTimeOffset.FromUnixTimeMilliseconds((long)x * 1000));
        var dateGen = Gen.Choose(1, 365_000)
            .Select(x => DateOnly.FromDayNumber(x));
        var headersGen = Gen.Constant(new Dictionary<string, string>
        {
            ["Authorization"] = "Bearer test-jwt-token",
            ["X-Housemate-Id"] = Guid.NewGuid().ToString()
        });

        return methodGen.SelectMany(method =>
            urlGen.SelectMany(url =>
                headersGen.SelectMany(headers =>
                    bodyGen.SelectMany(body =>
                        createdAtGen.SelectMany(createdAt =>
                            dateGen.SelectMany(date =>
                                mutationTypeGen.Select(mutationType =>
                                    new QueuedMutation(
                                        0,
                                        string.Empty,
                                        method,
                                        url,
                                        headers,
                                        body,
                                        createdAt,
                                        date,
                                        mutationType))))))));
    }

    private static Mock<IJSRuntime> CreateJsRuntimeMock(Queue<JsonElement> queue, Func<int> nextId)
    {
        var mock = new Mock<IJSRuntime>();

        // isAvailable returns true.
        mock.Setup(x => x.InvokeAsync<bool>(
                "window.happieCache.isAvailable",
                It.IsAny<object[]>()))
            .ReturnsAsync(true);

        // initialize is a no-op.
        mock.Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                "window.happieCache.initialize",
                It.IsAny<object[]>()))
            .ReturnsAsync(Mock.Of<Microsoft.JSInterop.Infrastructure.IJSVoidResult>());

        // enqueueMutation adds to the queue with an auto-incremented id.
        mock.Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                "window.happieCache.enqueueMutation",
                It.IsAny<object[]>()))
            .Callback<string, object[]>((_, args) =>
            {
                // args[0] = householdId, args[1] = mutationObject.
                var mutationJson = JsonSerializer.Serialize(args[1]);
                using var document = JsonDocument.Parse(mutationJson);
                var root = document.RootElement;

                // Build a new object with the auto-incremented id.
                var stored = new Dictionary<string, object?>
                {
                    ["id"] = nextId(),
                    ["method"] = root.GetProperty("method").GetString(),
                    ["url"] = root.GetProperty("url").GetString(),
                    ["body"] = root.TryGetProperty("body", out var bodyElement) && bodyElement.ValueKind != JsonValueKind.Null
                        ? bodyElement.GetString()
                        : null,
                    ["createdAt"] = root.GetProperty("createdAt").GetInt64(),
                    ["date"] = root.GetProperty("date").GetString(),
                    ["mutationType"] = root.GetProperty("mutationType").GetString(),
                    ["headers"] = JsonSerializer.Deserialize<Dictionary<string, string>>(
                        root.GetProperty("headers").GetRawText())
                };

                var storedJson = JsonSerializer.Serialize(stored);
                using var storedDoc = JsonDocument.Parse(storedJson);
                queue.Enqueue(storedDoc.RootElement.Clone());
            })
            .ReturnsAsync(Mock.Of<Microsoft.JSInterop.Infrastructure.IJSVoidResult>());

        // dequeueMutation removes from the front.
        mock.Setup(x => x.InvokeAsync<JsonElement?>(
                "window.happieCache.dequeueMutation",
                It.IsAny<object[]>()))
            .ReturnsAsync(() =>
            {
                if (queue.Count == 0)
                    return null;
                return queue.Dequeue();
            });

        return mock;
    }
}
