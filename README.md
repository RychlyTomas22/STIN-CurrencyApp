# CurrencyApp

An ASP.NET Core application for viewing and analysing exchange rates. It has a web interface, a separate API, shared analysis code and automated tests.

The dashboard lets a signed-in demo user select a base currency, target currencies and a date range. The API obtains current and historical rates, caches snapshots in files and calculates the highest and lowest current rate among the selected currencies and their average historical rates. The API can use either an external exchange-rate service or built-in mock data.

## Known limitations

- The dashboard's “strongest” and “weakest” labels currently correspond to the largest and smallest numerical exchange-rate quotes. They are not a meaningful comparison of the currencies' relative strength.
- The file-backed live cache has no expiry policy, so a saved “current” rate can become stale.
- The external provider request URL currently includes the configured access key and is written to application logs. Use only a disposable demo key with this version and avoid sharing those logs.

## Projects

- `src/CurrencyApp.Web` – MVC interface with cookie-based demo login, dashboard and settings.
- `src/CurrencyApp.Api` – endpoints for rates, analysis, settings and health; file-backed cache and settings storage.
- `src/CurrencyApp.Core` – exchange-rate models and analysis service.
- `tests/CurrencyApp.Tests` – xUnit tests for controllers, mapping, clients, storage and analysis.

The API requires an internal key for `/api` endpoints other than `/api/health`. The web app passes that key to the API. Demo login credentials are configured under `DemoUser` in the web app's settings.

## Build and test

Requires the .NET 8 SDK. From the repository root:

```bash
dotnet build STIN-CurrencyApp.sln
dotnet test STIN-CurrencyApp.sln
```

## Run locally

The web and API projects run as **two separate processes**. Supply the same local internal API key to both processes; keep real keys out of committed configuration. The following example uses PowerShell and the built-in mock rate provider:

API terminal:

```powershell
$env:InternalApiAuth__ApiKey = "local-demo-key"
$env:ExchangeRateHost__UseMockData = "true"
dotnet run --project src/CurrencyApp.Api --launch-profile https
```

Web terminal:

```powershell
$env:BackendApi__BaseUrl = "https://localhost:7179/"
$env:BackendApi__ApiKey = "local-demo-key"
dotnet run --project src/CurrencyApp.Web --launch-profile https
```

With the checked-in HTTPS launch profiles, open the web app at `https://localhost:7108/`. A local ASP.NET Core development certificate may need to be trusted. The demo account is defined in `src/CurrencyApp.Web/appsettings.json`.

For live rates, disable `ExchangeRateHost:UseMockData` and configure `ExchangeRateHost:AccessKey` for the external provider. The mock provider generates deterministic sample rates for USD, EUR, CZK, GBP, JPY, PLN and CHF; they are not market data.
