using System.ComponentModel.DataAnnotations;
using Happie.Api.Constants;
using Happie.Shared.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Happie.Api.Http;

/// <summary>Centralises request body deserialisation, null-checking, and DataAnnotations validation for Azure Functions.</summary>
public static class RequestValidator
{
    /// <summary>
    /// Deserialises the request body to <typeparamref name="T"/>, checks for null, and validates
    /// DataAnnotations attributes. Returns a <see cref="ReadResult{T}"/> that is either successful
    /// with the body, or failed with an <see cref="IActionResult"/> ready to return.
    /// </summary>
    public static async Task<ReadResult<T>> ReadAndValidateAsync<T>(HttpRequest req, CancellationToken ct)
        where T : class
    {
        T? body;
        try
        {
            body = await req.ReadFromJsonAsync<T>(ct);
        }
        catch
        {
            return ReadResult<T>.Fail(new BadRequestObjectResult(new ApiErrorResponse("Invalid request body.", ApiErrorCodes.BadRequest)));
        }

        if (body is null)
            return ReadResult<T>.Fail(new BadRequestObjectResult(new ApiErrorResponse("Request body is required.", ApiErrorCodes.BadRequest)));

        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(body, new ValidationContext(body), results, validateAllProperties: true))
        {
            var message = results[0].ErrorMessage ?? "Validation failed.";
            return ReadResult<T>.Fail(new UnprocessableEntityObjectResult(new ApiErrorResponse(message, ApiErrorCodes.ValidationError)));
        }

        return ReadResult<T>.Ok(body);
    }
}
