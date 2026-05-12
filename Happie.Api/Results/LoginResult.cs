using Happie.Api.Domain;
using Happie.Shared.Domain;

namespace Happie.Api.Results;

/// <summary>Result returned by a successful login attempt.</summary>
public record LoginResult(
    string Token,
    IReadOnlyList<Housemate> Housemates
);
