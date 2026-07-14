namespace Happie.Api.Domain;

/// <summary>A reusable dish description saved at the household level.</summary>
public record SavedDish(
    Guid Id,
    Guid HouseholdId,
    string Description,
    bool IsDeleted);
