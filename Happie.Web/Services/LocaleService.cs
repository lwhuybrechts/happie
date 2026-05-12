using Happie.Shared.Domain;
using Microsoft.JSInterop;

namespace Happie.Web.Services;

/// <summary>Manages the active locale, persists it to localStorage, and notifies subscribers when it changes.</summary>
public class LocaleService
{
    private const string LocaleStorageKey = "locale";

    private readonly IJSRuntime _jsRuntime;

    /// <summary>Raised whenever the active locale changes.</summary>
    public event Action? LocaleChanged;

    /// <summary>The currently active locale.</summary>
    public Locale CurrentLocale { get; private set; } = LocaleExtensions.Default;

    public LocaleService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <summary>Reads the persisted locale from localStorage and sets CurrentLocale. Defaults to Dutch when not set.</summary>
    public async Task InitializeAsync()
    {
        var storedCode = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", LocaleStorageKey);
        CurrentLocale = storedCode.ToLocale();
    }

    /// <summary>Persists the new locale to localStorage, updates CurrentLocale, and raises LocaleChanged.</summary>
    public async Task SetLocaleAsync(Locale locale)
    {
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", LocaleStorageKey, locale.ToCultureCode());
        CurrentLocale = locale;
        LocaleChanged?.Invoke();
    }
}
