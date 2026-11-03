# RSS Label Maker

Native Windows desktop label-design and printing application by Razeen Secure Solution.

## Implemented in the current feature branch

- Native C# / .NET 10 WPF desktop shell with RSS-branded navigation and dashboard.
- About page describing Razeen Secure Solution, its services and RSS Label Maker.
- Contact page with validated name, email, phone, subject and message fields. Submit opens the user's default email application with a pre-addressed draft to riyaz@rssapps.net; the user confirms delivery by pressing Send.
- Product creation, local SQLite persistence, edit, delete and reload.
- English, Sinhala and Tamil product-name fields.
- M.F.D, EXP.D, Batch Number and Net Weight fields with four independent **Print on Pack** controls. Each enabled field displays the exact text "Printed on Pack" in the live preview; other fields remain independent.
- Sugar, Salt and Fat values per 100g, M.R.P. and Import By text field.
- Dedicated Import By / Distributed By profile manager: save, edit and delete reusable company/person profiles with full addresses and optional contact details in the local SQLite database.
- Box pricing fields, Pieces per Box and optional per-piece price. Automatic mode validates the quantity and calculates Box Price ÷ Pieces per Box; manual mode stores its own value without overwriting it when the mode changes.
- Reference-inspired food-label preview with the three nutrition panels.
- Custom label width/height entry and nominal A4/A5/Letter row/column estimate.
- Windows-installed printer discovery, a 100 mm calibration-page print action, and a first product-label print renderer for the selected saved product. The renderer uses the Designer's label dimensions, product copies, nutrition panels, Print on Pack text, Import By / Distributed By details, and the saved watermark opacity.
- Multi-page print preview with A4, A5, Letter and custom paper dimensions, portrait/landscape orientation, independently adjustable top/bottom/left/right margins, horizontal/vertical label gaps, automatic rows/columns, page navigation and multi-page print submission. Preview and print share the same rendered sheet visuals.
- Logo watermark image selection, local binary persistence in SQLite, remove action and continuously adjustable 0–100% opacity setting. The current watermark preview is not yet integrated into final label print/PDF rendering.

## Account registration and offline use

- Mandatory first-time registration and email verification are part of the approved product requirements.
- Authentication, registration, verification, licensing, device activation, shared-user seats and entitlement checks are managed centrally by RSS Core (`https://rsscore.cv`). The desktop application must not create a separate account database or authentication Worker.
- Product records, label designs, templates, watermark assets and print jobs remain on the PC in SQLite by default. Backup archives are encrypted locally and uploaded to the user's Google Drive only after explicit authorization.
- Offline label work and printing are limited by the cached signed RSS Core entitlement and configured offline grace period. The device credential must be protected with Windows Credential Manager / DPAPI.
- Verified accounts receive a 14-day Premium trial with an RSS TRIAL watermark on every printed/PDF page. There is no permanent Free plan. After trial expiry, printing and Premium features require a paid license key validated by RSS Core; local data remains intact.
- RSS Core API capabilities must be checked against its implemented and deployed contract before the desktop client enables each feature. Do not assume a route is live merely because it appears in a requirements document.
- Detailed RSS Core API integration, known existing endpoints and missing server-side capabilities are documented in [docs/RSS-CORE-INTEGRATION.md](docs/RSS-CORE-INTEGRATION.md). Licensing, device activation, quota enforcement and Google Drive backup are specified in `REQUIREMENTS.md`.

## Proposed RLM license pricing

All prices are proposals, configurable by RSS Core Admin, and are not activated or hard-coded. The plans support annual (12-month) and lifetime (one-time) licenses in LKR and USD:

| Plan | Annual LKR / USD | Lifetime LKR / USD |
|---|---:|---:|
| Premium — Single PC (1 PC) | 5,000 / $25 | 12,000 / $60 |
| Premium — Shared License (1 owner, 3 PCs) | 9,000 / $50 | 22,000 / $120 |
| Premium — Multiple PC (Network, 5 PCs) | 18,000 / $100 | 40,000 / $220 |

The 14-day Premium trial includes full Premium features with an RSS TRIAL watermark on every printed/PDF page. No permanent Free plan exists. Shared License means one owner using multiple registered PCs; Multiple PC (Network) means PCs in a shared local-network workspace with separately controlled user seats. Premium printing includes 300/600 DPI options where supported by the printer.

## Company contact details

- Website: https://www.rssapps.net
- Phone: +94 77 115 5504 | +94 70 155 5504
- Email: riyaz@rssapps.net

## Current implementation limitations

This is an active development branch, not a release-ready application. The following approved requirements remain incomplete:

- Assigning saved Import By / Distributed By profiles to products, automatically populating company names and addresses, with product-level overrides.
- Persisted, editable user templates; watermark settings and image assets must be stored per template.
- Full designer editing (drag, resize, alignment, layers, undo/redo, barcode and QR).
- Accurate physical sheet layout including margins, gaps, printer printable-area validation and partially-used-sheet starting position.
- Physical print acceptance testing, printer-specific printable-area validation, and calibration against real Windows printer drivers. The custom page preview/print code has not yet been compiled or verified on a Windows printer.
- PDF export is a trial/Premium-entitlement feature. Trial PDF pages must carry the RSS TRIAL watermark; expired trials require a paid license. The PDF renderer and entitlement gate are not implemented yet.
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

This is a native WPF application, not a WebView wrapper. RSS Core is required for registration, verification, trial entitlement and paid licensing. Already-authorized PCs may retain bounded offline functionality using cached signed entitlements.

See [REQUIREMENTS.md](REQUIREMENTS.md) for the approved product specification.

## Development workflow

Work is being performed on `feature/live-print-on-pack-preview`. Keep `main` untouched. Do not deploy this desktop application. Do not report a Windows build, launch, printer, PDF or translation test as passed unless it has actually been run and verified on a suitable Windows environment.

- Product editor profile selection: Import By and Distributed By dropdowns use saved profiles. Selecting a profile fills editable product-level name/address fields; edits are stored as product snapshots so later master-profile changes do not silently overwrite existing products. The selected profile IDs and product-level details are persisted with each product. Label preview includes both parties and their addresses.
