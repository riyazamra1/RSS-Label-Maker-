# RSS Label Maker

Native Windows desktop label-design and printing application by Razeen Secure Solution.

## Implemented in the current feature branch

- Native C# / .NET 10 WPF desktop shell with RSS-branded navigation and dashboard.
- Product creation, local SQLite persistence, edit, delete and reload.
- English, Sinhala and Tamil product-name fields.
- M.F.D, EXP.D, Batch Number and Net Weight fields with four independent **Print on Pack** controls. Each enabled field displays the exact text "Printed on Pack" in the live preview; other fields remain independent.
- Sugar, Salt and Fat values per 100g, M.R.P. and Import By text field.
- Reference-inspired food-label preview with the three nutrition panels.
- Custom label width/height entry and nominal A4/A5/Letter row/column estimate.
- Windows-installed printer discovery and a 100 mm calibration-page print action.
- Logo watermark image selection, local binary persistence in SQLite, remove action and continuously adjustable 0–100% opacity setting. The current watermark preview is not yet integrated into final label print/PDF rendering.

## Current implementation limitations

This is an active development branch, not a release-ready application. The following approved requirements remain incomplete:

- Reusable Import By and Distributed By company/person profiles with full addresses and product assignment.
- Pieces-per-box, box price, optional per-piece price, and automatic/manual pricing modes.
- Persisted, editable user templates; watermark settings and image assets must be stored per template.
- Full designer editing (drag, resize, alignment, layers, undo/redo, barcode and QR).
- Accurate physical sheet layout including margins, gaps, printer printable-area validation and partially-used-sheet starting position.
- Final label rendering for physical printing and PDF export. The existing print action is a calibration page only.
- Excel/CSV import with mapping and row validation.
- Online Sinhala/Tamil translation provider and local translation cache.
- Database backup/restore, automated tests, installer and Windows 10/11 acceptance testing.

## Requirements

- Windows 10 or Windows 11; validate the installed .NET desktop runtime/SDK against the target machine.
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

See [REQUIREMENTS.md](REQUIREMENTS.md) for the approved product specification.

## Development workflow

Work is being performed on `feature/live-print-on-pack-preview`. Keep `main` untouched. Do not deploy this desktop application. Do not report a Windows build, launch, printer, PDF or translation test as passed unless it has actually been run and verified on a suitable Windows environment.
