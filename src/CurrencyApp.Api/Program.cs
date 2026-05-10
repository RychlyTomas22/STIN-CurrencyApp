using CurrencyApp.Api.Configuration;
using CurrencyApp.Api.Services;
using CurrencyApp.Api.Mappings;
using CurrencyApp.Core.Services;
using Serilog;
using System.IO;

var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables()
    .Build();

var logPath = configuration["LoggingStorage:Path"] ?? "data/logs/log-.txt";
var resolvedLogPath = Path.IsPathRooted(logPath)
    ? logPath
    : Path.Combine(Directory.GetCurrentDirectory(), logPath);

var logDirectory = Path.GetDirectoryName(resolvedLogPath);
if (!string.IsNullOrWhiteSpace(logDirectory))
{
    Directory.CreateDirectory(logDirectory);
}

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .WriteTo.File(
        resolvedLogPath,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        shared: true)
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<ExchangeRateHostOptions>(
    builder.Configuration.GetSection("ExchangeRateHost"));

builder.Services.AddHttpClient("ExchangeRateHost", (serviceProvider, client) =>
{
    var options = serviceProvider
        .GetRequiredService<Microsoft.Extensions.Options.IOptions<ExchangeRateHostOptions>>()
        .Value;

    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
});

builder.Services.Configure<UserSettingsStorageOptions>(
    builder.Configuration.GetSection("UserSettingsStorage"));

builder.Services.Configure<LoggingStorageOptions>(
    builder.Configuration.GetSection("LoggingStorage"));

var useMockExchangeRateData = builder.Configuration.GetValue<bool>("ExchangeRateHost:UseMockData");

builder.Services.Configure<ExchangeRateCacheOptions>(options =>
{
    var cacheRootPath = useMockExchangeRateData
        ? builder.Configuration["ExchangeRateCache:MockRootPath"]
        : builder.Configuration["ExchangeRateCache:RealRootPath"];

    if (string.IsNullOrWhiteSpace(cacheRootPath))
    {
        cacheRootPath = useMockExchangeRateData
            ? "data/exchange-rate-cache-mock"
            : "data/exchange-rate-cache-real";
    }

    options.RootPath = cacheRootPath;
});

if (useMockExchangeRateData)
{
    builder.Services.AddScoped<IExchangeRateHostClient, MockExchangeRateHostClient>();
}
else
{
    builder.Services.AddScoped<IExchangeRateHostClient, ExchangeRateHostClient>();
}
builder.Services.AddScoped<IExchangeRateResponseMapper, ExchangeRateResponseMapper>();
builder.Services.AddScoped<ICurrencyAnalysisService, CurrencyAnalysisService>();
builder.Services.AddScoped<IExchangeRateCacheService, FileExchangeRateCacheService>();
builder.Services.AddScoped<IUserSettingsService, FileUserSettingsService>();

var app = builder.Build();

var internalApiKey = builder.Configuration["InternalApiAuth:ApiKey"]
    ?? throw new InvalidOperationException("Missing configuration value: InternalApiAuth:ApiKey");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.Use(async (context, next) =>
{
    var path = context.Request.Path;

    if (path.StartsWithSegments("/api") && !path.StartsWithSegments("/api/health"))
    {
        var providedApiKey = context.Request.Headers["X-Internal-Api-Key"].ToString();

        if (!string.Equals(providedApiKey, internalApiKey, StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Unauthorized." });
            return;
        }
    }

    await next();
});

app.MapControllers();

try
{
    Log.Information("Starting CurrencyApp.Api");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "CurrencyApp.Api terminated");
}
finally
{
    Log.CloseAndFlush();
}