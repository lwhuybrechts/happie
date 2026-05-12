using Happie.Api.Results;

namespace Happie.Api.Handlers;

/// <summary>Handles household login requests.</summary>
public interface ILoginHandler
{
    /// <summary>
    /// Attempts to log in with the given password.
    /// Returns a <see cref="LoginResult"/> on success, or null when the password does not match any household.
    /// </summary>
    Task<LoginResult?> HandleAsync(string password, CancellationToken ct = default);
}
