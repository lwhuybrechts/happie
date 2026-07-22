namespace Happie.Web.Tests.Helpers;

/// <summary>HttpMessageHandler that supports async/delayed responses via TaskCompletionSource.</summary>
public class DelayableHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

    public DelayableHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
    {
        _handler = handler;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return _handler(request);
    }
}
