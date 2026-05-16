using System.Net;
using System.Net.Http.Json;

namespace Happie.Web.Tests.Helpers;

/// <summary>Simple HTTP message handler that returns a fixed status code and optional JSON body.</summary>
public sealed class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _statusCode;
    private readonly object? _responseContent;

    public MockHttpMessageHandler(HttpStatusCode statusCode, object? responseContent = null)
    {
        _statusCode = statusCode;
        _responseContent = responseContent;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(_statusCode);

        if (_responseContent is not null)
            response.Content = JsonContent.Create(_responseContent);

        return Task.FromResult(response);
    }
}
