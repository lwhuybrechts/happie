using System.ComponentModel.DataAnnotations;

namespace Happie.Api.Options;

/// <summary>Options for JWT signing, bound from the JwtSigningKey Key Vault secret.</summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>The Key Vault secret name used to populate this option at startup.</summary>
    public const string KeyVaultSecretName = "JwtSigningKey";

    [Required(ErrorMessage = "Jwt:SigningKey is required.")]
    public string SigningKey { get; set; } = string.Empty;
}
