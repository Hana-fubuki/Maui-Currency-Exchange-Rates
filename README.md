# Maui Currency Exchange Rates

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![.NET MAUI](https://img.shields.io/badge/.NET%20MAUI-10-512BD4?logo=dotnet)](https://learn.microsoft.com/dotnet/maui/)
[![CI](https://github.com/Hana-fubuki/Maui-Currency-Exchange-Rates/actions/workflows/ci.yml/badge.svg)](https://github.com/Hana-fubuki/Maui-Currency-Exchange-Rates/actions/workflows/ci.yml)
[![CodeQL](https://github.com/Hana-fubuki/Maui-Currency-Exchange-Rates/actions/workflows/codeql.yml/badge.svg)](https://github.com/Hana-fubuki/Maui-Currency-Exchange-Rates/actions/workflows/codeql.yml)
[![OpenSSF Scorecard](https://api.scorecard.dev/projects/github.com/Hana-fubuki/Maui-Currency-Exchange-Rates/badge)](https://scorecard.dev/viewer/?uri=github.com/Hana-fubuki/Maui-Currency-Exchange-Rates)
[![OpenSSF](https://img.shields.io/badge/OpenSSF-security%20checks%20enabled-3fb950)](https://github.com/Hana-fubuki/Maui-Currency-Exchange-Rates/actions/workflows/scorecards.yml)

A .NET MAUI currency converter that pulls live and historical exchange-rate data from the **Frankfurter API** and presents it in a Google-inspired desktop/mobile UI.

## What it does

- Shows the latest exchange rate for a selected currency pair.
- Supports two-way amount conversion, so editing either side recalculates the other.
- Displays historical rate data with range presets such as **1D**, **5D**, **1M**, **1Y**, **5Y**, and **Max**.
- Renders an interactive chart with hover details and drag-to-select range comparisons.
- Uses lightweight caching so repeated currency and rate requests do not constantly hit the API.

## What it uses

| Area | Details |
| --- | --- |
| UI | .NET MAUI, XAML, Shell |
| Language | C# / .NET 10 |
| Data source | [Frankfurter API](https://frankfurter.dev/) |
| Architecture | ViewModel + service layer |
| Caching | In-memory cache (`MemoryCacheService`) |
| Charts | Custom `GraphicsView`-based chart rendering |
| Tests | xUnit + Coverlet collector |

## Repository layout

| Path | Purpose |
| --- | --- |
| `CurrencyExchangeRates\` | MAUI application |
| `CurrencyExchangeRates\Features\Exchange\` | Exchange screen UI, chart, and view model |
| `CurrencyExchangeRates\Services\` | API client, cache, and exchange service |
| `CurrencyExchangeRates.Tests\` | Unit tests for shared logic |
| `.github\workflows\` | CI, CodeQL, and OpenSSF workflows |

## Prerequisites

### General

1. Install **.NET SDK 10.0.x**.
2. Clone the repository.
3. Restore MAUI workloads and project dependencies.

```powershell
git clone https://github.com/Hana-fubuki/Maui-Currency-Exchange-Rates.git
cd Maui-Currency-Exchange-Rates
dotnet workload restore .\CurrencyExchangeRates\CurrencyExchangeRates.csproj
dotnet restore
```

### Platform requirements

| Target | Host OS | Extra requirements |
| --- | --- | --- |
| Windows | Windows 10/11 | MAUI Windows workload and Windows desktop build tools |
| Android | Windows or macOS | Android SDK, emulator/device, and Java toolchain |
| iOS | macOS only | Xcode, iOS Simulator/device tooling, signing setup |
| MacCatalyst | macOS only | Xcode and MacCatalyst tooling |

> **Note:** iOS and MacCatalyst builds must be done on a Mac. Windows can build and run the Windows target, and can also build Android if the Android toolchain is installed.

## Run locally

### Windows

```powershell
dotnet run --project .\CurrencyExchangeRates\CurrencyExchangeRates.csproj -f net10.0-windows10.0.19041.0
```

### Android

With an emulator or device connected:

```powershell
dotnet build .\CurrencyExchangeRates\CurrencyExchangeRates.csproj -f net10.0-android
dotnet build .\CurrencyExchangeRates\CurrencyExchangeRates.csproj -t:Run -f net10.0-android
```

### iOS

On macOS:

```powershell
dotnet build .\CurrencyExchangeRates\CurrencyExchangeRates.csproj -f net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64
dotnet build .\CurrencyExchangeRates\CurrencyExchangeRates.csproj -t:Run -f net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64
```

For Intel-based simulators, use `iossimulator-x64` instead.

### MacCatalyst

On macOS:

```powershell
dotnet build .\CurrencyExchangeRates\CurrencyExchangeRates.csproj -f net10.0-maccatalyst -p:RuntimeIdentifier=maccatalyst-arm64
dotnet build .\CurrencyExchangeRates\CurrencyExchangeRates.csproj -t:Run -f net10.0-maccatalyst -p:RuntimeIdentifier=maccatalyst-arm64
```

For Intel Macs, use `maccatalyst-x64`.

## Build runtime-specific outputs

### Windows

```powershell
dotnet build .\CurrencyExchangeRates\CurrencyExchangeRates.csproj -c Release -f net10.0-windows10.0.19041.0
dotnet publish .\CurrencyExchangeRates\CurrencyExchangeRates.csproj -c Release -f net10.0-windows10.0.19041.0
```

### Android

```powershell
dotnet build .\CurrencyExchangeRates\CurrencyExchangeRates.csproj -c Release -f net10.0-android
dotnet publish .\CurrencyExchangeRates\CurrencyExchangeRates.csproj -c Release -f net10.0-android
```

### iOS

```powershell
dotnet build .\CurrencyExchangeRates\CurrencyExchangeRates.csproj -c Release -f net10.0-ios -p:RuntimeIdentifier=ios-arm64
dotnet publish .\CurrencyExchangeRates\CurrencyExchangeRates.csproj -c Release -f net10.0-ios -p:RuntimeIdentifier=ios-arm64
```

### MacCatalyst

```powershell
dotnet build .\CurrencyExchangeRates\CurrencyExchangeRates.csproj -c Release -f net10.0-maccatalyst -p:RuntimeIdentifier=maccatalyst-arm64
dotnet publish .\CurrencyExchangeRates\CurrencyExchangeRates.csproj -c Release -f net10.0-maccatalyst -p:RuntimeIdentifier=maccatalyst-arm64
```

## Test and coverage

Run the test suite:

```powershell
dotnet test .\CurrencyExchangeRates.Tests\CurrencyExchangeRates.Tests.csproj
```

Run tests with coverage:

```powershell
dotnet test .\CurrencyExchangeRates.Tests\CurrencyExchangeRates.Tests.csproj --collect:"XPlat Code Coverage" -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.IncludeTestAssembly=true
```

The test project compiles the shared app source files directly so unit tests can run without spinning up the MAUI Windows app host.

## GitHub Actions

This repository includes:

- **CI**: restores workloads, builds the Windows target, runs tests, and uploads test/coverage artifacts.
- **CodeQL**: scans the repository for C# security and code-quality issues.
- **OpenSSF Scorecard**: runs repository security posture analysis and uploads SARIF results.
- **Dependency Review**: checks dependency changes on pull requests.

## API source

Exchange-rate data is provided by [Frankfurter](https://frankfurter.dev/).
