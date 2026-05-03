using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using CurrencyApp.Api.Configuration;
using CurrencyApp.Api.Services;
using CurrencyApp.Api.Mappings;
using CurrencyApp.Core.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddScoped<IExchangeRateHostClient, ExchangeRateHostClient>();
builder.Services.AddScoped<IExchangeRateResponseMapper, ExchangeRateResponseMapper>();
builder.Services.AddScoped<ICurrencyAnalysisService, CurrencyAnalysisService>();
builder.Services.AddScoped<IExchangeRateCacheService, FileExchangeRateCacheService>();

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

app.Run();