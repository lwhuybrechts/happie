using System.Net;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace Happie.Web.Tests.Helpers;

/// <summary>Extension methods for bUnit test setup.</summary>
public static class BunitContextExtensions
{
    /// <summary>Registers an HttpClient that returns a fixed status code and optional JSON body for all requests.</summary>
    public static void RegisterHttpClient(this BunitContext context, HttpStatusCode statusCode, object? responseContent = null)
    {
        var httpClient = new HttpClient(new MockHttpMessageHandler(statusCode, responseContent))
        {
            BaseAddress = new Uri("http://localhost/api/"),
        };
        context.Services.AddSingleton(httpClient);
    }
}
