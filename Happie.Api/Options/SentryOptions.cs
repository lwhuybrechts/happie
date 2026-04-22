using System.ComponentModel.DataAnnotations;

namespace Happie.Api.Options;

/// <summary>Options for Sentry error monitoring, bound from the SentryDsn Key Vault secret.</summary>
public class SentryOptions
{
    public const string SectionName = "Sentry";

    [Required(ErrorMessage = "Sentry:Dsn is required.")]
    public string Dsn { get; set; } = string.Empty;
}
