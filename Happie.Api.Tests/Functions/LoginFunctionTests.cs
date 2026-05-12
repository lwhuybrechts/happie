using ExpectedObjects;
using Happie.Api.Functions;
using Happie.Api.Handlers;
using Happie.Api.Models;
using Happie.Shared.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Text;
using System.Text.Json;

namespace Happie.Api.Tests.Functions;

/// <summary>Unit tests for <see cref="LoginFunction"/>.</summary>
public class LoginFunctionTests
{
    private readonly Mock<ILoginHandler> _loginHandlerMock = new();
    private readonly LoginFunction _sut;

    /// <summary>Initializes a new instance of <see cref="LoginFunctionTests"/> with a mocked login handler.</summary>
    public LoginFunctionTests()
    {
        _sut = new LoginFunction(_loginHandlerMock.Object);
    }

    /// <summary>Correct password returns HTTP 200 with a token and housemate list.</summary>
    [Fact]
    public async Task Run_CorrectPassword_ReturnsOkWithTokenAndHousemates()
    {
        // Arrange.
        var housemateId = Guid.NewGuid();
        var token = "signed-jwt-token";

        var loginResult = CreateLoginResult(token, housemateId);
        SetupHandleAsync("correct-password", loginResult);

        var request = CreateRequest(new { Password = "correct-password" });

        // Act.
        var result = await _sut.Run(request, CancellationToken.None);

        // Assert.
        var response = Assert.IsType<LoginResponse>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(token, response.Token);

        new HousemateDto(housemateId, "Alice", HousemateColors.Palette[0])
            .ToExpectedObject()
            .ShouldEqual(Assert.Single(response.Housemates));
    }

    /// <summary>Incorrect password returns HTTP 401 with UNAUTHORIZED code.</summary>
    [Fact]
    public async Task Run_IncorrectPassword_ReturnsUnauthorized()
    {
        // Arrange.
        SetupHandleAsync("wrong-password", null);

        var request = CreateRequest(new { Password = "wrong-password" });

        // Act.
        var result = await _sut.Run(request, CancellationToken.None);

        // Assert.
        var json = JsonSerializer.Serialize(Assert.IsType<UnauthorizedObjectResult>(result).Value);
        Assert.Contains("UNAUTHORIZED", json);
    }

    /// <summary>Empty password body returns HTTP 400.</summary>
    [Fact]
    public async Task Run_EmptyPassword_ReturnsBadRequest()
    {
        // Arrange.
        var request = CreateRequest(new { Password = "" });

        // Act.
        var result = await _sut.Run(request, CancellationToken.None);

        // Assert.
        Assert.IsType<BadRequestObjectResult>(result);
    }

    /// <summary>Null body returns HTTP 400.</summary>
    [Fact]
    public async Task Run_NullBody_ReturnsBadRequest()
    {
        // Arrange.
        var request = CreateRequest<object?>(null);

        // Act.
        var result = await _sut.Run(request, CancellationToken.None);

        // Assert.
        Assert.IsType<BadRequestObjectResult>(result);
    }

    private void SetupHandleAsync(string password, LoginResult? returns)
    {
        _loginHandlerMock
            .Setup(x => x.HandleAsync(password, It.IsAny<CancellationToken>()))
            .ReturnsAsync(returns);
    }

    private static LoginResult CreateLoginResult(string token, Guid housemateId) =>
        new(token, new List<Housemate>
        {
            new(housemateId, Guid.NewGuid(), "Alice", HousemateColors.Palette[0], false),
        });

    private static HttpRequest CreateRequest<T>(T body)
    {
        var json = JsonSerializer.Serialize(body);
        var bytes = Encoding.UTF8.GetBytes(json);

        var context = new DefaultHttpContext();
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream(bytes);
        context.Request.ContentLength = bytes.Length;

        return context.Request;
    }
}
