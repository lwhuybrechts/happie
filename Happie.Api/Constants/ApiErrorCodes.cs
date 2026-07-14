namespace Happie.Api.Constants;

/// <summary>Machine-readable error codes returned in API error responses.</summary>
public static class ApiErrorCodes
{
    /// <summary>The request body was missing or malformed.</summary>
    public const string BadRequest = "BAD_REQUEST";

    /// <summary>Authentication credentials were missing or invalid.</summary>
    public const string Unauthorized = "UNAUTHORIZED";

    /// <summary>The authenticated caller does not have permission to perform the action.</summary>
    public const string Forbidden = "FORBIDDEN";

    /// <summary>The requested resource was not found.</summary>
    public const string NotFound = "NOT_FOUND";

    /// <summary>The request payload failed validation.</summary>
    public const string ValidationError = "VALIDATION_ERROR";

    /// <summary>The requested housemate color is already in use within the household.</summary>
    public const string ColorConflict = "COLOR_CONFLICT";

    /// <summary>The resource has been modified since the specified timestamp.</summary>
    public const string Conflict = "CONFLICT";

    /// <summary>A saved dish with the same description already exists in the household.</summary>
    public const string DishAlreadyExists = "DISH_ALREADY_EXISTS";

    /// <summary>An unexpected server-side error occurred.</summary>
    public const string InternalError = "INTERNAL_ERROR";
}
