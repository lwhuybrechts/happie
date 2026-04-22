using Happie.Api.Options;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using HappieSentryOptions = Happie.Api.Options.SentryOptions;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

// Register SentryOptions with startup validation.
builder.Services
    .Configure<HappieSentryOptions>(builder.Configuration.GetSection(HappieSentryOptions.SectionName))
    .AddOptionsWithValidateOnStart<HappieSentryOptions>();

// Register Sentry as an ILogger provider; DSN is read from SentryOptions at startup.
// All ILogger.Log* calls and unhandled exceptions flow to Sentry automatically.
builder.Logging.AddSentry(o =>
{
    o.Dsn = builder.Configuration[$"{HappieSentryOptions.SectionName}:{nameof(HappieSentryOptions.Dsn)}"];
    o.MinimumBreadcrumbLevel = LogLevel.Information;
    o.MinimumEventLevel = LogLevel.Warning;
});

builder.Build().Run();
