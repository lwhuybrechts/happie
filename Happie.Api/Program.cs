using Azure.Data.Tables;
using Azure.Identity;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Security.KeyVault.Secrets;
using Happie.Api.Handlers;
using Happie.Api.Infrastructure;
using Happie.Api.Infrastructure.Mappers;
using Happie.Api.Infrastructure.Repositories;
using Happie.Api.Middleware;
using Happie.Api.Options;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using HappieSentryOptions = Happie.Api.Options.SentryOptions;
using Happie.Api.Infrastructure.Mappers;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// FunctionsApplicationBuilder implements IHostApplicationBuilder, which exposes
// Configuration as an IConfigurationManager (which also implements IConfigurationBuilder).
// This is the correct way to add Key Vault as a configuration source in the
// FunctionsApplication.CreateBuilder pattern.
// In Azure, DefaultAzureCredential resolves via Managed Identity.
// Locally, authenticate via `az login`.
var keyVaultUri = builder.Configuration["KeyVaultUri"];
if (!string.IsNullOrWhiteSpace(keyVaultUri))
{
    var credential = new DefaultAzureCredential();
    var secretClient = new SecretClient(new Uri(keyVaultUri), credential);
    builder.Configuration.AddAzureKeyVault(secretClient, new KeyVaultSecretManager());
}

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

// Register TableServiceClient and the ITableStorageClient wrapper.
// The connection string is resolved from Key Vault at runtime.
builder.Services.AddSingleton(x =>
{
    var config = x.GetRequiredService<IConfiguration>();
    return new TableServiceClient(config["TableStorageConnectionString"]);
});
builder.Services.AddSingleton<ITableStorageClient, TableStorageClient>();

// Register all mappers as singletons.
builder.Services.AddSingleton<IHouseholdMapper, HouseholdMapper>();
builder.Services.AddSingleton<IHousemateMapper, HousemateMapper>();
builder.Services.AddSingleton<IAttendanceRecordMapper, AttendanceRecordMapper>();
builder.Services.AddSingleton<IDishRecordMapper, DishRecordMapper>();
builder.Services.AddSingleton<ICommentMapper, CommentMapper>();
builder.Services.AddSingleton<IDayHistoryEntryMapper, DayHistoryEntryMapper>();
builder.Services.AddSingleton<IPushSubscriptionMapper, PushSubscriptionMapper>();

// Register all repositories as singletons.
builder.Services.AddSingleton<IHouseholdRepository, HouseholdRepository>();
builder.Services.AddSingleton<IHousemateRepository, HousemateRepository>();
builder.Services.AddSingleton<IAttendanceRepository, AttendanceRepository>();
builder.Services.AddSingleton<IDishRepository, DishRepository>();
builder.Services.AddSingleton<ICommentRepository, CommentRepository>();
builder.Services.AddSingleton<IDayHistoryRepository, DayHistoryRepository>();
builder.Services.AddSingleton<IPushSubscriptionRepository, PushSubscriptionRepository>();

// Register SentryOptions with startup validation.
builder.Services
    .Configure<HappieSentryOptions>(builder.Configuration.GetSection(HappieSentryOptions.SectionName))
    .AddOptionsWithValidateOnStart<HappieSentryOptions>();

// Register JwtOptions with startup validation.
// JwtSigningKey is loaded from Key Vault as a flat secret and mapped to JwtOptions.SigningKey.
builder.Services
    .Configure<JwtOptions>(x => x.SigningKey = builder.Configuration["JwtSigningKey"] ?? string.Empty)
    .AddOptionsWithValidateOnStart<JwtOptions>();

// Register authentication handlers.
builder.Services.AddSingleton<ILoginHandler, LoginHandler>();

// Register Sentry as an ILogger provider; DSN is read from SentryOptions at startup.
// All ILogger.Log* calls and unhandled exceptions flow to Sentry automatically.
builder.Logging.AddSentry(x =>
{
    x.Dsn = builder.Configuration[$"{HappieSentryOptions.SectionName}:{nameof(HappieSentryOptions.Dsn)}"];
    x.MinimumBreadcrumbLevel = LogLevel.Information;
    x.MinimumEventLevel = LogLevel.Warning;
});

// Register JWT authentication middleware.
builder.UseMiddleware<JwtMiddleware>();

builder.Build().Run();
