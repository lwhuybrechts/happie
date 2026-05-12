namespace Happie.Api.Constants;

/// <summary>Keys used to store validated identity values in <see cref="Microsoft.Azure.Functions.Worker.FunctionContext.Items"/>.</summary>
public static class FunctionContextKeys
{
    /// <summary>Key for the validated household ID (Guid).</summary>
    public const string HouseholdId = "HouseholdId";

    /// <summary>Key for the validated housemate ID (Guid).</summary>
    public const string HousemateId = "HousemateId";
}
