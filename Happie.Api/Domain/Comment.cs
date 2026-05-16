namespace Happie.Api.Domain;

/// <summary>A free-text note from a housemate for a specific day (one slot per housemate per day).</summary>
public record Comment(
    Guid HouseholdId,
    Guid HousemateId,
    DateOnly Date,
    // Max 200 chars.
    string Text,
    DateTimeOffset? LastEditedAt
);
