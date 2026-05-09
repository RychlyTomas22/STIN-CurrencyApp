using CurrencyApp.Web.Configuration;
using CurrencyApp.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.Configure<DemoUserOptions>(
    builder.Configuration.GetSection("DemoUser"));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "CurrencyApp.Auth";
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
    });

builder.Services.AddAuthorization();

builder.Services.AddHttpClient("BackendApi", client =>
{
    var baseUrl = builder.Configuration["BackendApi:BaseUrl"]
        ?? throw new InvalidOperationException("Missing configuration value: BackendApi:BaseUrl");

    baseUrl = baseUrl.Trim();

    if (!baseUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
        !baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
    {
        baseUrl = "https://" + baseUrl;
    }

    if (!baseUrl.EndsWith("/"))
    {
        baseUrl += "/";
    }

    var apiKey = builder.Configuration["BackendApi:ApiKey"]
        ?? throw new InvalidOperationException("Missing configuration value: BackendApi:ApiKey");

    client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
    client.DefaultRequestHeaders.Add("X-Internal-Api-Key", apiKey);
});

builder.Services.AddScoped<IBackendApiClient, BackendApiClient>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();