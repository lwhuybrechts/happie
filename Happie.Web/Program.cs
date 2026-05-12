using System.Globalization;
using Happie.Shared.Domain;
using Happie.Web;
using Happie.Web.Http;
using Happie.Web.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Register the delegating handler that injects JWT and X-Housemate-Id headers.
builder.Services.AddTransient<AuthHeaderHandler>();

// Register the named HttpClient with base address pointing to the /api proxy.
builder.Services.AddHttpClient("HappieApi", client =>
{
    client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress + "api/");
}).AddHttpMessageHandler<AuthHeaderHandler>();

// Register a typed HttpClient factory so components can resolve HttpClient directly.
builder.Services.AddScoped(serviceProvider =>
{
    var factory = serviceProvider.GetRequiredService<IHttpClientFactory>();
    return factory.CreateClient("HappieApi");
});

// Register localization services and resource files.
builder.Services.AddLocalization();

// Register the locale service as scoped so it is shared within a single render session.
builder.Services.AddScoped<LocaleService>();

var host = builder.Build();

// Initialize locale from localStorage before rendering so the correct culture is active from the first render.
var jsRuntime = host.Services.GetRequiredService<IJSRuntime>();
var storedLocale = await jsRuntime.InvokeAsync<string?>("localStorage.getItem", "locale");
var culture = new CultureInfo(storedLocale.ToLocale().ToCultureCode());
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

await host.RunAsync();
