using System.Diagnostics.CodeAnalysis;
using Happie.Api.Constants;
using Happie.Shared.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace Happie.Api.Http;

/// <summary>Parses and validates route parameters for Azure Functions.</summary>
public static class RouteParser
{
    /// <summary>
    /// Parses a date string in yyyy-MM-dd format.
    /// Returns true and sets <paramref name="date"/> on success.
    /// Returns false and sets <paramref name="error"/> to a 400 result on failure.
    /// </summary>
    public static bool TryParseDate(
        string value,
        [NotNullWhen(true)] out DateOnly date,
        [NotNullWhen(false)] out IActionResult? error)
    {
        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", out date))
        {
            error = null;
            return true;
        }

        error = new BadRequestObjectResult(new ApiErrorResponse("Date must be in yyyy-MM-dd format.", ApiErrorCodes.BadRequest));
        return false;
    }

    /// <summary>
    /// Parses a GUID string.
    /// Returns true and sets <paramref name="id"/> on success.
    /// Returns false and sets <paramref name="error"/> to a 404 result on failure.
    /// </summary>
    public static bool TryParseGuid(
        string value,
        [NotNullWhen(true)] out Guid id,
        [NotNullWhen(false)] out IActionResult? error)
    {
        if (Guid.TryParse(value, out id))
        {
            error = null;
            return true;
        }

        error = new NotFoundObjectResult(new ApiErrorResponse("Housemate not found.", ApiErrorCodes.NotFound));
        return false;
    }
}
