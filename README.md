# RSS Label Maker

Native Windows desktop label-design and printing application by Razeen Secure Solution.

## Current implementation

The first WPF interface foundation includes:
- RSS-branded desktop navigation and dashboard.
- Product-entry form for English, Sinhala and Tamil names, batch/date/weight, M.R.P. and Sugar/Salt/Fat per 100g.
- A reference-inspired food-label canvas with the three nutrition panels.
- Built-in template selection screen.
- Print Manager shell with Windows-installed printer discovery.
- A4-oriented designer workspace.

**Important:** This is the first UI foundation, not a complete release. Product entries currently live only in the running session. SQLite persistence, translation API/cache, editable canvas elements, dimension calculations, real print rendering, PDF and bulk import remain subsequent milestones. The print action is intentionally not represented as complete yet.

## Requirements

- Windows 10 or Windows 11 (verify the installed .NET desktop runtime/SDK against the target machine).
- .NET 10 SDK.
- Visual Studio 2022 with .NET desktop development workload, or the .NET CLI on Windows.

## Build on Windows

```powershell
dotnet --info
dotnet restore .\RSS.LabelMaker.sln
dotnet build .\RSS.LabelMaker.sln -c Release
dotnet run --project .\src\RSS.LabelMaker.App\RSS.LabelMaker.App.csproj
```

This is a native WPF application, not a WebView wrapper. Core label-making works offline without RSS Core or forced sign-in. Automatic English-to-Sinhala/Tamil transliteration has five free uses and then requires Premium; manual Sinhala/Tamil entry remains free and offline.

See [REQUIREMENTS.md](REQUIREMENTS.md) for the approved product specification.
