using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Mvc;

namespace Happie.Api.Http;

/// <summary>Represents the outcome of deserialising and validating an HTTP request body.</summary>
public sealed class ReadResult<T> where T : class
{
    /// <summary>The deserialised and validated body. Non-null when <see cref="IsSuccess"/> is true.</summary>
    public T? Body { get; }

    /// <summary>The error result to return to the caller. Non-null when <see cref="IsSuccess"/> is false.</summary>
    public IActionResult? Error { get; }

    /// <summary>True when the body was successfully deserialised and passed validation.</summary>
    [MemberNotNullWhen(true, nameof(Body))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    private ReadResult(T? body, IActionResult? error)
    {
        Body = body;
        Error = error;
    }

    /// <summary>Creates a successful result with the given body.</summary>
    public static ReadResult<T> Ok(T body) => new(body, null);

    /// <summary>Creates a failed result with the given error action result.</summary>
    public static ReadResult<T> Fail(IActionResult error) => new(null, error);
}
