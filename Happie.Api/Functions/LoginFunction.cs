using Happie.Api.Constants;
using Happie.Api.Handlers;
using Happie.Api.Http;
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
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/login")] HttpRequest request,
        CancellationToken cancellationToken)
    {
        var readResult = await RequestValidator.ReadAndValidateAsync<LoginRequest>(request, cancellationToken);
        if (!readResult.IsSuccess)
            return readResult.Error;

        var loginResult = await _loginHandler.HandleAsync(readResult.Body.Password, cancellationToken);

        if (loginResult is null)
            return new UnauthorizedObjectResult(new ApiErrorResponse("Invalid password.", ApiErrorCodes.Unauthorized));

        var housemates = loginResult.Housemates
            .Select(x => new HousemateDto(x.Id, x.Name, x.Color))
            .ToList();

        return new OkObjectResult(new LoginResponse(loginResult.Token, housemates));
    }
}
