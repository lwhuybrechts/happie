using System.Text.Json;
using Microsoft.JSInterop;

namespace Happie.Web.Services.Caching;

/// <summary>Wraps JS interop calls for IndexedDB mutation queue operations.</summary>
public class MutationQueue : IMutationQueue
{
    private readonly IJSRuntime _jsRuntime;
    private bool _isAvailable;

    public MutationQueue(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        try
        {
            _isAvailable = await _jsRuntime.InvokeAsync<bool>("window.happieCache.isAvailable");
            if (_isAvailable)
                await _jsRuntime.InvokeVoidAsync("window.happieCache.initialize");
        }
        catch (JSException)
        {
            _isAvailable = false;
        }
    }

    /// <inheritdoc />
    public async Task EnqueueAsync(string householdId, QueuedMutation mutation)
    {
        if (!_isAvailable)
            return;

        try
        {
            var mutationObject = new
            {
                method = mutation.Method,
                url = mutation.Url,
                headers = mutation.Headers,
                body = mutation.Body,
                createdAt = mutation.CreatedAt.ToUnixTimeMilliseconds(),
                date = mutation.Date.ToString("yyyy-MM-dd"),
                mutationType = mutation.MutationType
            };

            await _jsRuntime.InvokeVoidAsync("window.happieCache.enqueueMutation", householdId, mutationObject);
        }
        catch (JSException)
        {
            // IndexedDB operation failed; degrade gracefully.
        }
    }

    /// <inheritdoc />
    public async Task<QueuedMutation?> DequeueAsync(string householdId)
    {
        if (!_isAvailable)
            return null;

        try
        {
            var result = await _jsRuntime.InvokeAsync<JsonElement?>("window.happieCache.dequeueMutation", householdId);
            if (result is null || result.Value.ValueKind == JsonValueKind.Null)
                return null;

            return DeserializeMutation(result.Value, householdId);
        }
        catch (JSException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<QueuedMutation>> PeekAllAsync(string householdId)
    {
        if (!_isAvailable)
            return Array.Empty<QueuedMutation>();

        try
        {
            var result = await _jsRuntime.InvokeAsync<JsonElement>("window.happieCache.peekAllMutations", householdId);
            if (result.ValueKind != JsonValueKind.Array)
                return Array.Empty<QueuedMutation>();

            var mutations = new List<QueuedMutation>();
            foreach (var element in result.EnumerateArray())
            {
                mutations.Add(DeserializeMutation(element, householdId));
            }

            return mutations;
        }
        catch (JSException)
        {
            return Array.Empty<QueuedMutation>();
        }
    }

    private static QueuedMutation DeserializeMutation(JsonElement element, string householdId)
    {
        var id = element.GetProperty("id").GetInt32();
        var method = element.GetProperty("method").GetString() ?? string.Empty;
        var url = element.GetProperty("url").GetString() ?? string.Empty;
        var body = element.TryGetProperty("body", out var bodyElement) && bodyElement.ValueKind != JsonValueKind.Null
            ? bodyElement.GetString()
            : null;
        var createdAt = DateTimeOffset.FromUnixTimeMilliseconds(element.GetProperty("createdAt").GetInt64());
        var date = DateOnly.Parse(element.GetProperty("date").GetString() ?? string.Empty);
        var mutationType = element.GetProperty("mutationType").GetString() ?? string.Empty;

        var headers = new Dictionary<string, string>();
        if (element.TryGetProperty("headers", out var headersElement) && headersElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in headersElement.EnumerateObject())
            {
                headers[property.Name] = property.Value.GetString() ?? string.Empty;
            }
        }

        return new QueuedMutation(id, householdId, method, url, headers, body, createdAt, date, mutationType);
    }
}
