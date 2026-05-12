namespace Happie.Shared.Contracts;

/// <summary>Describes a push delivery failure for a single recipient.</summary>
public record NudgeFailureDto(
    Guid RecipientHousemateId,
    string Reason
);
