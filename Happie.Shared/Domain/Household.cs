namespace Happie.Shared.Domain;

/// <summary>A group of housemates sharing a single Happie instance, identified by a unique password.</summary>
public record Household(
    Guid Id,
    string Name,
    // bcrypt hash of the household password.
    string PasswordHash
);
