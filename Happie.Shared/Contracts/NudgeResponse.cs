namespace Happie.Shared.Contracts;

/// <summary>Response body for a nudge request, containing per-recipient delivery failures.</summary>
public record NudgeResponse(
    IReadOnlyList<NudgeFailureDto> Failures
);
