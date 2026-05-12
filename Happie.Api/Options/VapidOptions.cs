using System.ComponentModel.DataAnnotations;

namespace Happie.Api.Options;

/// <summary>Options for VAPID Web Push, bound from Key Vault secrets.</summary>
public class VapidOptions
{
    public const string SectionName = "Vapid";

    [Required(ErrorMessage = "Vapid:PublicKey is required.")]
    public string PublicKey { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vapid:PrivateKey is required.")]
    public string PrivateKey { get; set; } = string.Empty;
}
