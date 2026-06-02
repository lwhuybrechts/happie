using System.Text.Json;
using ExpectedObjects;
using Happie.Api.Domain;
using Happie.Api.Handlers;
using Happie.Shared.Domain;

namespace Happie.Api.Tests.Handlers;

/// <summary>Unit tests for <see cref="ParameterNameResolver"/>.</summary>
public class ParameterNameResolverTests
{
    /// <summary>When the "name" value is a valid GUID matching a housemate, it is resolved to the current name.</summary>
    [Fact]
    public void Resolve_NameIsGuidMatchingHousemate_ReturnsResolvedName()
    {
        // Arrange.
        var housemateId = Guid.NewGuid();
        var housemateById = CreateHousemateDict(housemateId, "Alice", false);
        var parametersJson = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["name"] = housemateId.ToString(),
            ["status"] = "EatingIn"
        });

        // Act.
        var result = ParameterNameResolver.Resolve(parametersJson, housemateById);

        // Assert.
        var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(result)!;
        new Dictionary<string, string> { ["name"] = "Alice", ["status"] = "EatingIn" }
            .ToExpectedObject()
            .ShouldEqual(parsed);
    }

    /// <summary>When the "name" value is a valid GUID for a soft-deleted housemate, the name includes "(deleted)".</summary>
    [Fact]
    public void Resolve_NameIsGuidOfDeletedHousemate_ReturnsNameWithDeletedSuffix()
    {
        // Arrange.
        var housemateId = Guid.NewGuid();
        var housemateById = CreateHousemateDict(housemateId, "Bob", true);
        var parametersJson = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["name"] = housemateId.ToString()
        });

        // Act.
        var result = ParameterNameResolver.Resolve(parametersJson, housemateById);

        // Assert.
        var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(result)!;
        Assert.Equal("Bob (deleted)", parsed["name"]);
    }

    /// <summary>When the "name" value is a GUID not found in the lookup, it resolves to an empty string.</summary>
    [Fact]
    public void Resolve_NameIsGuidNotInLookup_ReturnsEmptyString()
    {
        // Arrange.
        var unknownId = Guid.NewGuid();
        var housemateById = new Dictionary<Guid, Housemate>();
        var parametersJson = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["name"] = unknownId.ToString()
        });

        // Act.
        var result = ParameterNameResolver.Resolve(parametersJson, housemateById);

        // Assert.
        var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(result)!;
        Assert.Equal(string.Empty, parsed["name"]);
    }

    /// <summary>When the "name" value is a plain string (legacy format), it is left unchanged.</summary>
    [Fact]
    public void Resolve_NameIsPlainString_ReturnsUnchanged()
    {
        // Arrange.
        var housemateById = new Dictionary<Guid, Housemate>();
        var parametersJson = """{"name":"Alice","status":"EatingIn"}""";

        // Act.
        var result = ParameterNameResolver.Resolve(parametersJson, housemateById);

        // Assert.
        var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(result)!;
        Assert.Equal("Alice", parsed["name"]);
    }

    /// <summary>When there is no "name" key in the parameters, the JSON is returned unchanged.</summary>
    [Fact]
    public void Resolve_NoNameKey_ReturnsUnchanged()
    {
        // Arrange.
        var housemateById = new Dictionary<Guid, Housemate>();
        var parametersJson = """{"description":"Pizza"}""";

        // Act.
        var result = ParameterNameResolver.Resolve(parametersJson, housemateById);

        // Assert.
        var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(result)!;
        new Dictionary<string, string> { ["description"] = "Pizza" }
            .ToExpectedObject()
            .ShouldEqual(parsed);
    }

    /// <summary>When the input is null, it is returned as-is.</summary>
    [Fact]
    public void Resolve_NullInput_ReturnsNull()
    {
        // Arrange.
        var housemateById = new Dictionary<Guid, Housemate>();

        // Act.
        var result = ParameterNameResolver.Resolve(null!, housemateById);

        // Assert.
        Assert.Null(result);
    }

    /// <summary>When the input is an empty string, it is returned as-is.</summary>
    [Fact]
    public void Resolve_EmptyString_ReturnsEmpty()
    {
        // Arrange.
        var housemateById = new Dictionary<Guid, Housemate>();

        // Act.
        var result = ParameterNameResolver.Resolve(string.Empty, housemateById);

        // Assert.
        Assert.Equal(string.Empty, result);
    }

    /// <summary>When the input is malformed JSON, it is returned as-is.</summary>
    [Fact]
    public void Resolve_MalformedJson_ReturnsOriginal()
    {
        // Arrange.
        var housemateById = new Dictionary<Guid, Housemate>();
        var malformed = "not valid json {{{";

        // Act.
        var result = ParameterNameResolver.Resolve(malformed, housemateById);

        // Assert.
        Assert.Equal(malformed, result);
    }

    /// <summary>Other parameters besides "name" are preserved unchanged.</summary>
    [Fact]
    public void Resolve_OtherParametersPreserved_ReturnsAllKeys()
    {
        // Arrange.
        var housemateId = Guid.NewGuid();
        var housemateById = CreateHousemateDict(housemateId, "Charlie", false);
        var parametersJson = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["name"] = housemateId.ToString(),
            ["text"] = "Looks great!",
            ["status"] = "EatingIn"
        });

        // Act.
        var result = ParameterNameResolver.Resolve(parametersJson, housemateById);

        // Assert.
        var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(result)!;
        new Dictionary<string, string> { ["name"] = "Charlie", ["text"] = "Looks great!", ["status"] = "EatingIn" }
            .ToExpectedObject()
            .ShouldEqual(parsed);
    }

    private static Dictionary<Guid, Housemate> CreateHousemateDict(Guid housemateId, string name, bool isDeleted) =>
        new()
        {
            [housemateId] = new Housemate(housemateId, Guid.NewGuid(), name, HousemateColors.Palette[0], isDeleted),
        };
}
