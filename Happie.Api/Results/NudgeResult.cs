using Happie.Shared.Contracts;

namespace Happie.Api.Results;

/// <summary>The result of a nudge operation, containing per-recipient delivery failures.</summary>
public record NudgeResult(IReadOnlyList<NudgeFailureDto> Failures);
