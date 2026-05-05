using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
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

var keyRingPath = builder.Configuration["SharedAuth:KeyRingPath"]
                  ?? throw new InvalidOperationException(
                      "Missing configuration value: SharedAuth:KeyRingPath");

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keyRingPath))
    .SetApplicationName("CurrencyApp");

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "CurrencyApp.Auth";

        options.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            },
            OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }
        };
    });

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

builder.Services.Configure<ExchangeRateCacheOptions>(
    builder.Configuration.GetSection("ExchangeRateCache"));

var webAppOrigin = builder.Configuration["Cors:WebAppOrigin"]
                   ?? throw new InvalidOperationException("Missing configuration value: Cors:WebAppOrigin");

builder.Services.AddCors(options =>
{
    options.AddPolicy("WebClient", policy =>
    {
        policy.WithOrigins(webAppOrigin)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.Configure<UserSettingsStorageOptions>(
    builder.Configuration.GetSection("UserSettingsStorage"));

builder.Services.Configure<LoggingStorageOptions>(
    builder.Configuration.GetSection("LoggingStorage"));

builder.Services.AddScoped<IExchangeRateHostClient, ExchangeRateHostClient>();
builder.Services.AddScoped<IExchangeRateResponseMapper, ExchangeRateResponseMapper>();
builder.Services.AddScoped<ICurrencyAnalysisService, CurrencyAnalysisService>();
builder.Services.AddScoped<IExchangeRateCacheService, FileExchangeRateCacheService>();
builder.Services.AddScoped<IUserSettingsService, FileUserSettingsService>();

builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseCors("WebClient");
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