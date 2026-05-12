using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Happie.Api.Constants;
using Happie.Api.Models;
using Happie.Api.Options;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Happie.Api.Middleware;

/// <summary>
/// Middleware that validates the JWT bearer token and the X-Housemate-Id header on every
/// protected Azure Function. The login endpoint is exempt from authentication.
/// </summary>
public class JwtMiddleware : IFunctionsWorkerMiddleware
{
    private const string AnonymousRoute = "auth/login";
    private const string BearerPrefix = "Bearer ";
    private const string HousemateIdHeader = "X-Housemate-Id";
    private const string HouseholdIdClaim = "householdId";

    private readonly JwtOptions _jwtOptions;

    /// <summary>Initializes a new instance of <see cref="JwtMiddleware"/>.</summary>
    public JwtMiddleware(IOptions<JwtOptions> jwtOptions)
    {
        _jwtOptions = jwtOptions.Value;
    }

    /// <inheritdoc/>
    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var requestData = await context.GetHttpRequestDataAsync();

        // Pass through when there is no HTTP request data (e.g. timer triggers).
        if (requestData is null)
        {
            await next(context);
            return;
        }

        // Skip authentication for the anonymous login endpoint.
        if (IsAnonymousRoute(requestData.Url.AbsolutePath))
        {
            await next(context);
            return;
        }

        // Validate the Authorization: Bearer JWT.
        var authHeader = requestData.Headers.TryGetValues("Authorization", out var authValues)
            ? authValues.FirstOrDefault()
            : null;

        if (!TryExtractBearerToken(authHeader, out var token))
        {
            await WriteErrorAsync(context, requestData, 401, "Missing or invalid Authorization header.", ApiErrorCodes.Unauthorized);
            return;
        }

        if (!TryValidateToken(token, out var householdId))
        {
            await WriteErrorAsync(context, requestData, 401, "Invalid or expired token.", ApiErrorCodes.Unauthorized);
            return;
        }

        // Validate the X-Housemate-Id header.
        var housemateIdHeader = requestData.Headers.TryGetValues(HousemateIdHeader, out var housemateValues)
            ? housemateValues.FirstOrDefault()
            : null;

        if (!TryParseHousemateId(housemateIdHeader, out var housemateId))
        {
            await WriteErrorAsync(context, requestData, 401, "Missing or invalid X-Housemate-Id header.", ApiErrorCodes.Unauthorized);
            return;
        }

        // Attach validated identity to the function context for downstream handlers.
        context.Items[FunctionContextKeys.HouseholdId] = householdId;
        context.Items[FunctionContextKeys.HousemateId] = housemateId;

        await next(context);
    }

    /// <summary>Returns true when the request path targets the anonymous login route.</summary>
    internal static bool IsAnonymousRoute(string absolutePath)
    {
        var path = absolutePath.TrimStart('/');

        // Strip the leading "api/" prefix added by the Azure Functions host.
        if (path.StartsWith("api/", StringComparison.OrdinalIgnoreCase))
            path = path["api/".Length..];

        return path.Equals(AnonymousRoute, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Extracts the bearer token from an Authorization header value.
    /// Returns true on success and sets <paramref name="token"/>.
    /// </summary>
    internal static bool TryExtractBearerToken(string? authHeader, out string token)
    {
        token = string.Empty;

        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
            return false;

        token = authHeader[BearerPrefix.Length..].Trim();
        return !string.IsNullOrWhiteSpace(token);
    }

    /// <summary>
    /// Validates the JWT and extracts the household ID claim.
    /// Returns true on success and sets <paramref name="householdId"/>.
    /// </summary>
    internal bool TryValidateToken(string token, out Guid householdId)
    {
        householdId = Guid.Empty;

        var keyBytes = Encoding.UTF8.GetBytes(_jwtOptions.SigningKey);
        var securityKey = new SymmetricSecurityKey(keyBytes);

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = securityKey,
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
        };

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, validationParameters, out _);

            var claim = principal.FindFirst(HouseholdIdClaim);
            if (claim is null || !Guid.TryParse(claim.Value, out householdId))
                return false;

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Parses the X-Housemate-Id header value as a GUID.
    /// Returns true on success and sets <paramref name="housemateId"/>.
    /// </summary>
    internal static bool TryParseHousemateId(string? headerValue, out Guid housemateId)
    {
        housemateId = Guid.Empty;
        return !string.IsNullOrWhiteSpace(headerValue) && Guid.TryParse(headerValue, out housemateId);
    }

    /// <summary>Writes a JSON error response and short-circuits the middleware pipeline.</summary>
    private static async Task WriteErrorAsync(
        FunctionContext context,
        HttpRequestData requestData,
        int statusCode,
        string message,
        string code)
    {
        var response = requestData.CreateResponse();
        response.StatusCode = (System.Net.HttpStatusCode)statusCode;
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteAsJsonAsync(new ApiErrorResponse(message, code));

        // Bind the response to the function invocation result so the pipeline is short-circuited.
        var invocationResult = context.GetInvocationResult();
        invocationResult.Value = response;
    }
}
