namespace Happie.Api.Domain;

/// <summary>An authorized user of Happie within a household.</summary>
public record Housemate(
    Guid Id,
    Guid HouseholdId,
    string Name,
    // Hex code from the predefined palette, e.g. "#E91E63".
    string Color,
    bool IsDeleted,
    int SortOrder = 0,
    string? AppVersion = null
);
