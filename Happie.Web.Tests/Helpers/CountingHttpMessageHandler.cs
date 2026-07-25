using System.Net;

namespace Happie.Web.Tests.Helpers;

/// <summary>HttpMessageHandler that counts the number of HTTP requests sent.</summary>
public sealed class CountingHttpMessageHandler : HttpMessageHandler
{
    public int CallCount { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
    }
}
