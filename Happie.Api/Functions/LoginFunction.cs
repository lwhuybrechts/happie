using Happie.Api.Constants;
using Happie.Api.Handlers;
using Happie.Api.Results;
using Happie.Shared.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Happie.Api.Functions;

/// <summary>Azure Function that handles household login.</summary>
public class LoginFunction
{
    private readonly ILoginHandler _loginHandler;

    /// <summary>Initializes a new instance of <see cref="LoginFunction"/>.</summary>
    public LoginFunction(ILoginHandler loginHandler)
    {
        _loginHandler = loginHandler;
    }

    /// <summary>Validates the household password and returns a signed JWT with the active housemate list.</summary>
    [Function("Login")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/login")] HttpRequest req,
        CancellationToken ct)
    {
        LoginRequest? body;
        try
        {
            body = await req.ReadFromJsonAsync<LoginRequest>(ct);
        }
        catch
        {
            return new BadRequestObjectResult(new ApiErrorResponse("Invalid request body.", ApiErrorCodes.BadRequest));
        }

        if (body is null || string.IsNullOrWhiteSpace(body.Password))
            return new BadRequestObjectResult(new ApiErrorResponse("Password is required.", ApiErrorCodes.BadRequest));

        var result = await _loginHandler.HandleAsync(body.Password, ct);

        if (result is null)
            return new UnauthorizedObjectResult(new ApiErrorResponse("Invalid password.", ApiErrorCodes.Unauthorized));

        var housemates = result.Housemates
            .Select(h => new HousemateDto(h.Id, h.Name, h.Color))
            .ToList();

        return new OkObjectResult(new LoginResponse(result.Token, housemates));
    }
}
