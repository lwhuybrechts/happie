using System.Text.Json;
using Happie.Api.Domain;

namespace Happie.Api.Handlers;

/// <summary>
/// Resolves housemate IDs stored in history parameter JSON to current display names.
/// For backward compatibility, values that are not valid GUIDs are left as-is (legacy entries stored plain names).
/// </summary>
public static class ParameterNameResolver
{
    /// <summary>
    /// Replaces the "name" parameter value with the housemate's current display name if the value is a valid GUID.
    /// Soft-deleted housemates are formatted as "Name (deleted)".
    /// </summary>
    public static string Resolve(string parametersJson, Dictionary<Guid, Housemate> housemateById)
    {
        if (string.IsNullOrEmpty(parametersJson))
            return parametersJson;

        Dictionary<string, string>? parameters;
        try
        {
            parameters = JsonSerializer.Deserialize<Dictionary<string, string>>(parametersJson);
        }
        catch (JsonException)
        {
            return parametersJson;
        }

        if (parameters is null)
            return parametersJson;

        if (parameters.TryGetValue("name", out var nameValue) && Guid.TryParse(nameValue, out var housemateId))
        {
            if (housemateById.TryGetValue(housemateId, out var housemate))
                parameters["name"] = housemate.IsDeleted ? $"{housemate.Name} (deleted)" : housemate.Name;
            else
                parameters["name"] = string.Empty;
        }

        return JsonSerializer.Serialize(parameters);
    }
}
