using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Happie.Api.Tests.Functions;

/// <summary>Factory for creating <see cref="HttpRequest"/> instances in function tests.</summary>
internal static class HttpRequestFactory
{
    /// <summary>Creates an HTTP request with a JSON-serialized body.</summary>
    internal static HttpRequest Create<T>(T body)
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
