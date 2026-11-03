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

**Important:** This is the first UI foundation, not a complete release. Product entries currently live only in the running session. SQLite persistence, ChatGPT-backed Sinhala/Tamil name generation, editable canvas elements, dimension calculations, real print rendering, PDF and bulk import remain subsequent milestones. The print action is intentionally not represented as complete yet.

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

This is a native WPF application. It is not a WebView wrapper and does not require RSS Core, a server, sign-in or a subscription.

Core label workflows remain free and offline. Automatic English-to-Sinhala and English-to-Tamil transliteration/translation has a Free allowance of 5 successful uses per language per Asia/Colombo day; Premium provides unlimited automatic generation. Manual Sinhala/Tamil entry remains free and offline. See [REQUIREMENTS.md](REQUIREMENTS.md) and [the transliteration implementation contract](docs/TRANSLITERATION_IMPLEMENTATION.md) for the approved rules.
