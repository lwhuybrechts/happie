using Microsoft.JSInterop;

namespace Happie.Web.Services;

/// <summary>Provides access to the active housemate's identity (ID, name, color) by reading from localStorage.</summary>
public class ActiveHousemateService
{
    private const string IdStorageKey = "activeHousemateId";
    private const string NameStorageKey = "activeHousemateName";
    private const string ColorStorageKey = "activeHousemateColor";

    private readonly IJSRuntime _jsRuntime;

    public Guid? Id { get; private set; }
    public string? Name { get; private set; }
    public string? Color { get; private set; }

    /// <summary>Raised when the active housemate changes so UI components can re-render.</summary>
    public event Action? OnChanged;

    public ActiveHousemateService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <summary>Reads the active housemate data from localStorage.</summary>
    public async Task InitializeAsync()
    {
        var idString = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", IdStorageKey);

        if (!string.IsNullOrWhiteSpace(idString) && Guid.TryParse(idString, out var parsedId))
            Id = parsedId;

        Name = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", NameStorageKey);
        Color = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", ColorStorageKey);
    }

    /// <summary>Persists the active housemate's ID, name, and color to localStorage.</summary>
    public async Task SetActiveHousemateAsync(Guid id, string name, string color)
    {
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", IdStorageKey, id.ToString());
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", NameStorageKey, name);
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", ColorStorageKey, color);

        Id = id;
        Name = name;
        Color = color;

        OnChanged?.Invoke();
    }
}
