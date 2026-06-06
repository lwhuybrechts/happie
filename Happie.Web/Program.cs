using System.Globalization;
using Happie.Shared.Domain;
using Happie.Shared.Resources;
using Happie.Web;
using Happie.Web.Http;
using Happie.Web.Services;
using Happie.Web.Services.Caching;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Logging;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Initialize Sentry for client-side error monitoring.
var sentryDsn = builder.Configuration["SentryDsn"] ?? string.Empty;
builder.UseSentry(options =>
{
    options.Dsn = sentryDsn;
    options.SendDefaultPii = true;
});
builder.Logging.AddSentry(options => options.InitializeSdk = false);

// Register the delegating handler that injects JWT and X-Housemate-Id headers.
builder.Services.AddTransient<AuthHeaderHandler>();

// Register the named HttpClient with base address pointing to the /api proxy.
// In local development, appsettings.Development.json sets ApiBaseUrl to the Functions host directly.
// In production on Azure Static Web Apps, appsettings.json uses the relative "api/" proxy path.
var apiBaseUrl = builder.Configuration["ApiBaseUrl"]
    ?? throw new InvalidOperationException("ApiBaseUrl is not configured. Add it to appsettings.json or appsettings.Development.json.");
var apiBaseUri = apiBaseUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
    ? new Uri(apiBaseUrl)
    : new Uri(builder.HostEnvironment.BaseAddress + apiBaseUrl);

builder.Services.AddHttpClient("HappieApi", client =>
{
    client.BaseAddress = apiBaseUri;
}).AddHttpMessageHandler<AuthHeaderHandler>();

// Register a typed HttpClient factory so components can resolve HttpClient directly.
builder.Services.AddScoped(serviceProvider =>
{
    var factory = serviceProvider.GetRequiredService<IHttpClientFactory>();
    return factory.CreateClient("HappieApi");
});

// Register localization services and resource files.
builder.Services.AddLocalization();

// Register the shared string resolver for client-side history and nudge translation resolution.
builder.Services.AddSingleton<SharedStringResolver>();

// Register the locale service as scoped so it is shared within a single render session.
builder.Services.AddScoped<LocaleService>();

// Register the session service as scoped so it is shared within a single render session.
builder.Services.AddScoped<SessionService>();

// Register the active housemate service as scoped so it is shared within a single render session.
builder.Services.AddScoped<ActiveHousemateService>();

// Register the push notification service as scoped so it is shared within a single render session.
builder.Services.AddScoped<PushNotificationService>();

// Register the connectivity service as scoped for online/offline detection.
builder.Services.AddScoped<IConnectivityService, ConnectivityService>();

// Register the loading indicator state as scoped for tracking background operations.
builder.Services.AddScoped<IDelayService, RealDelayService>();
builder.Services.AddScoped<LoadingIndicatorState>();

// Register the mutation queue as scoped for offline mutation queueing.
builder.Services.AddScoped<IMutationQueue, MutationQueue>();

// Register the cache store as scoped for IndexedDB day plan and calendar caching.
builder.Services.AddScoped<ICacheStore, CacheStore>();

// Register the cached API client as scoped for stale-while-revalidate and offline mutations.
builder.Services.AddScoped<ICachedApiClient, CachedApiClient>();

// Register the sync toast state as scoped for managing sync failure toasts.
builder.Services.AddScoped<SyncToastState>();

// Register the sync service as scoped for replaying queued mutations on reconnect.
builder.Services.AddScoped<ISyncService, SyncService>();

var host = builder.Build();

// Initialize the LocaleService and set the thread culture before rendering so the correct locale is active from the first frame.
var localeService = host.Services.GetRequiredService<LocaleService>();
await localeService.InitializeAsync();
var culture = new CultureInfo(localeService.CurrentLocale.ToCultureCode());
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

// Initialize the ActiveHousemateService so the avatar is available from the first render.
var activeHousemateService = host.Services.GetRequiredService<ActiveHousemateService>();
await activeHousemateService.InitializeAsync();

// Initialize offline cache services (IndexedDB + connectivity detection).
var cacheStore = host.Services.GetRequiredService<ICacheStore>();
await cacheStore.InitializeAsync();

var mutationQueue = host.Services.GetRequiredService<IMutationQueue>();
await mutationQueue.InitializeAsync();

var connectivityService = host.Services.GetRequiredService<IConnectivityService>();
await connectivityService.InitializeAsync();

var syncService = host.Services.GetRequiredService<ISyncService>();
await syncService.InitializeAsync();

await host.RunAsync();
