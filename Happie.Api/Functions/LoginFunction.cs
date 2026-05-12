using Happie.Api.Handlers;
using Happie.Api.Models;
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
            return new BadRequestObjectResult(new { error = "Invalid request body.", code = "BAD_REQUEST" });
        }

        if (body is null || string.IsNullOrWhiteSpace(body.Password))
        {
            return new BadRequestObjectResult(new { error = "Password is required.", code = "BAD_REQUEST" });
        }

        var result = await _loginHandler.HandleAsync(body.Password, ct);

        if (result is null)
        {
            return new UnauthorizedObjectResult(new { error = "Invalid password.", code = "UNAUTHORIZED" });
        }

        var housemates = result.Housemates
            .Select(h => new HousemateDto(h.Id, h.Name, h.Color))
            .ToList();

        return new OkObjectResult(new LoginResponse(result.Token, housemates));
    }
}
