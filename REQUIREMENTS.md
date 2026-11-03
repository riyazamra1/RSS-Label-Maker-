# RSS Label Maker — Product Requirements Specification

**Product:** RSS Label Maker  
**Publisher:** Razeen Secure Solution  
**Repository:** riyazamra1/RSS-Label-Maker-  
**Target OS:** Windows 10 and Windows 11  
**Distribution:** Free tier + RSS Core-managed licensed subscriptions  
**Specification status:** Approved requirements baseline  
**Version target:** 1.0.0

## 1. Product vision

Build a native Windows desktop application for creating, managing, laying out and printing product labels. The primary built-in template must closely reproduce the user-provided Choco Shop food-label reference: white rectangular label, thin black border, red English product heading, Sinhala and Tamil product-name line, product/date information, M.R.P., three coloured nutrition panels (Sugar, Salt and Fat), and importer details at the bottom.

The reference is a layout guide, not a fixed-size template. Users must be able to change label and paper dimensions without redesigning the template.

## 2. Confirmed product decisions

- Native Windows desktop application; not a WebView wrapper.
- C# / .NET 10 LTS / WPF.
- SQLite local database.
- Mandatory account registration and email verification through RSS Core before first use.
- RSS Core is the central user database, identity provider, license authority and subscription/entitlement authority. Do not create a separate RSS Label Maker user database or standalone authentication backend.
- Free tier is available after registration and is subject to configurable daily label-print limits and feature restrictions. Paid plans are activated by redeeming a license key issued/validated by RSS Core.
- License entitlements govern device count, shared-user seats, feature access, daily usage limits, cloud backup and restore.
- Any printer installed and usable through the Windows printing subsystem.
- Local label work is stored in SQLite. Offline access is governed by a cached, signed, time-bounded RSS Core entitlement; usage limits and revocations must reconcile with RSS Core when online.
- Online translation is used only to generate Sinhala and Tamil product-name suggestions from an English product name. Translation results are cached locally.
- No automatic translation of any other field.
- No deployment requirement; deliver a Windows installer after validation.

## 2A. RSS Core accounts, licensing and device authorization — mandatory

### Central account database and identity
- The user must register and verify their email before entering RSS Label Maker.
- All account/profile details must be stored in the existing RSS Core user database. RSS Label Maker must not maintain a separate user/account database.
- RSS Core is the source of truth for account identity, email verification, account status, license keys, subscription entitlements, device activations, shared-user membership, usage quotas and audit events.
- The desktop app communicates with RSS Core over HTTPS using documented, versioned APIs. Email verification and password recovery emails are sent by RSS Core's existing transactional email mechanism; no separate RSS Label Maker Resend backend or D1 account database.
- Keep product records, label designs, templates, watermark assets and print jobs local by default. Cloud backup content is stored in the user's authorized Google Drive unless a later approved RSS Core storage feature is specified.

### License and subscription model
- A registered user receives the RSS Label Maker Free entitlement automatically. The Free entitlement has a configurable daily label-print quota and feature restrictions.
- Paid access is activated by entering a license key in the app. The key is validated and redeemed by RSS Core; the desktop app must never decide that a key is valid based only on local string format.
- RSS Core returns a signed entitlement describing product/app, account, plan, status, validity period, allowed device activations, allowed shared-user seats, daily print quota, feature flags and cloud-backup permissions.
- The established RSS Core payment architecture is retained: RSS Core owns the Payments module and integrates Payments.lk hosted checkout; no separate RSS Pay app/backend. Payment credentials remain server-side. Entitlements are activated only after a verified, idempotent payment webhook, never from a browser return URL.
- Previously accepted commercial baseline: a one-time payment for one year of RSS Premium entitlement (not recurring monthly billing). The actual price remains unselected. Do not invent prices or imply auto-renewal.
- License key redemption, renewal/replacement, expiry, refund/revocation and device deactivation are handled by RSS Core.
- Show plan name, license status, expiry/valid-through date, active devices, available device slots, shared-user seats and current day's usage in the app.

### Free tier restrictions
- Free users have a configurable maximum number of labels printed per account per day. Count successful label output, not preview actions; define duplicate/copy handling consistently in the RSS Core usage policy.
- PDF export is disabled for Free.
- Saving reusable Import By / Distributed By master profiles is disabled for Free. Product-specific importer/distributor text may be entered for an individual product only if the final plan policy permits it.
- Additional feature restrictions must be controlled by server-provided feature flags, not hard-coded assumptions. The plan matrix must be finalized before release.
- When a user reaches the daily quota, clearly show the reset time and offer license-key redemption / plan upgrade. Do not delete or lock existing local data.
- Quota enforcement must be resistant to clock rollback and local database edits. The server is authoritative when online; cached signed quota grants may support a bounded offline allowance.

### Device identity and license activation
- Generate a stable per-installation device ID and bind it to a privacy-preserving Windows device fingerprint. Do not transmit raw hardware serial numbers or sensitive hardware identifiers.
- RSS Core records each activated device against the user/license and enforces the plan's device limit.
- Single-PC license: one active PC device slot.
- Multi-PC license: multiple device slots according to the purchased entitlement.
- Device activation must support viewing device name/ID, last-seen time and deactivation from the account/device management UI.
- Reinstall, hardware change, OS reset and device replacement need a documented recovery/deactivation process; do not silently create unlimited new device slots.
- A device ID alone is not proof of license ownership. Use signed device-bound tokens and server-side activation records.

### Multi-user / shared-user licensing
- Support a license owner inviting additional RSS Core accounts as shared users, subject to the plan's shared-user seat count.
- Shared users authenticate with their own verified RSS Core accounts. Never share one owner's password or license key as a common login.
- RSS Core manages invitations, acceptance, removal, role/permission assignments and seat counts.
- Shared-user access must be scoped to the licensed RSS Label Maker workspace/business and its permitted data. Define roles such as Owner/Admin, Editor and Print-only; permissions must be enforced by the app and RSS Core APIs where applicable.
- Shared-user seats and PC device activations are separate entitlement counts. Do not treat one PC as one user or one user as one PC.

### Offline policy
- A device with a valid cached signed entitlement may continue permitted local work while offline, within a bounded offline grace period and cached daily quota.
- New registration, email verification, first activation, license-key redemption, adding a device, inviting shared users, cloud backup/restore and license changes require online RSS Core access.
- Offline usage must be recorded locally in a tamper-evident queue and reconciled with RSS Core on reconnection. Detect duplicate/replayed usage events.
- An offline PC cannot immediately learn of a server-side revocation, seat removal or subscription expiry. Do not claim immediate offline revocation.
- If the offline grace period or cached quota is exhausted, block only the restricted operations; preserve access to local data and allow viewing/exporting permitted local data as plan policy defines.
- Do not silently erase or lock customer data when a license expires.

### Google Drive backup and restore
- Provide explicit Backup to Google Drive and Restore from Google Drive actions.
- Use Google's supported OAuth 2.0 authorization flow with PKCE. Store refresh credentials securely using Windows Credential Manager/DPAPI; never embed a Google client secret in the desktop executable.
- Store backups in a clearly named RSS Label Maker folder in the user's Google Drive (or a dedicated app-data location only if the user explicitly chooses that behavior). Do not access unrelated Drive files.
- Back up the local application database, product records, templates, label designs, saved profiles where licensed, and referenced assets. Exclude passwords, RSS Core session tokens, license keys and Google OAuth secrets.
- Encrypt backup archives before upload; use authenticated encryption and a key derived from a user-held recovery secret or securely managed key flow. Do not upload plaintext customer data.
- Include backup version, application version, created timestamp, account/workspace ID and integrity hash. Validate integrity and compatibility before restore.
- Restore must offer a preview/confirmation and create a local safety backup before replacing or merging existing data.
- Support manual backup and restore first. Automatic/scheduled backup may be enabled only after the manual workflow is reliable.
- Google Drive backup entitlement and storage/backup limits are controlled by RSS Core plan flags. Do not claim RSS Core stores the backup payload unless separately approved.

### Security and API requirements
- All authentication, email verification, license validation, device activation, seat management, quota checks, usage reconciliation and entitlement retrieval must go through RSS Core.
- Desktop client stores only short-lived access credentials and a protected refresh/device credential; never store plaintext passwords or reusable bearer tokens in SQLite, logs or ordinary settings files.
- Use TLS, strict request validation, signed short-lived access tokens, token rotation/revocation, rate limits, generic auth errors and audit logging.
- Do not embed RSS Core secrets, Payments.lk credentials, Google client secrets or signing keys in the Windows binary.
- RSS Core must enforce ownership and authorization for every user/license/device/seat operation. Never trust client-supplied plan names, quota values, device counts or feature flags.
- API failures must not corrupt local data. Preserve bounded offline operation for already-authorized devices.

### Acceptance criteria
1. New users register and verify through RSS Core; user details appear in the RSS Core user database.
2. No separate Label Maker account database or standalone auth Worker/D1 is used.
3. Free plan is automatically assigned and its daily print quota is enforced.
4. Free plan cannot export PDF or save reusable Import By / Distributed By master profiles.
5. License key entry calls RSS Core and activates only a valid, eligible entitlement.
6. Single-PC and multi-PC activation limits are enforced by RSS Core using stable device IDs.
7. Shared users authenticate separately and consume authorized seat entitlements.
8. Offline access follows signed entitlement, grace-period and cached-quota rules.
9. Google Drive backup and restore use secure OAuth, encrypted archives and integrity validation.
10. Existing local data is preserved across expiry, sign-out, network outage and restore failures.
11. Payment fulfillment is based on verified Payments.lk webhook processing by RSS Core.
12. Tests cover quota boundaries, day rollover, clock rollback, duplicate usage, key redemption, device replacement, seat removal, offline expiry and backup/restore integrity.

## 3. Product information model

### 3.1 Product name

Three fields:
- Product Name — English (source input).
- Product Name — Sinhala (generated by online translation; editable).
- Product Name — Tamil (generated by online translation; editable).

When the English name changes, the application may offer an explicit Translate action. Do not send a request on every keystroke. Never overwrite a manually edited Sinhala or Tamil value without confirmation. Preserve cached results and allow manual entry when offline.

### 3.2 Other label fields

- Manufacturer Date (M.F.D).
- Expiry Date (EXP.D).
- Batch Number (B.No).
- Net Weight / Volume (N.Wt), with selectable units: kg, g, mg, L and ml.
- Ingredients (multiline).
- Warning (multiline).
- Import By — selected from reusable saved company/person records.
- Distributed By — selected from reusable saved company/person records.
- Maintain Import By and Distributed By master records in a dedicated **Import By & Distributed By** tab for plans whose entitlement allows reusable profile saving. Free users cannot save reusable Import By / Distributed By master profiles. Products may retain product-specific importer/distributor text according to the Free plan feature policy.
- Distributed By (company / person).
- Pieces per Box (positive whole-number quantity).
- Box Price (LKR / Rs.).
- Optional Per-Piece Price display checkbox. When unchecked, hide the per-piece price input and omit it from the label. When checked, show the per-piece price and allow a pricing mode:
  - Automatic: Per-Piece Price = Box Price ÷ Pieces per Box.
  - Manual: user enters the per-piece price; do not overwrite it with the calculated value.
- Validate Pieces per Box > 0 before automatic calculation. Store the pricing mode and manual value so switching modes does not silently destroy the user's entry.
- Saved Import By and Distributed By records each contain a name and full address. The selected record automatically populates the product's label details; product-specific overrides are allowed.
- Pieces (numeric quantity).
- M.R.P. (LKR / Rs.): Box Price and Per Piece Price.

Fields must be configurable for visibility in a template. The supplied reference specifically shows M.F.D, EXP.D, B.No, N.Wt, M.R.P. and Import By. Do not assume all product fields must be printed on every template.

- Add four independent **Print on Pack** checkboxes directly beside their respective product-entry fields: M.F.D, EXP.D, Batch Number and Net Weight. Do not use one shared checkbox for these four fields.
- Each checkbox controls only its matching field. When unchecked, print that field's actual value normally. When checked, suppress the actual value and print the exact text **Printed on Pack** in that field's position on the label. The other three fields must remain unaffected.
- Persist each checkbox state with the product/label configuration and apply it consistently in the live preview, saved/reloaded design, PDF export and physical print output. The four controls must remain independently editable.

## 4. Nutrition panel — required

Every food-label product record must support three independent nutrition values:
- Sugar: numeric value, normally displayed as g/100g.
- Salt: numeric value, normally displayed as g/100g.
- Fat: numeric value, normally displayed as g/100g.

Default display units are g/100g, but store the numeric value and unit explicitly. Example values visible in the reference image are Sugar 49g/100g, Salt 0.24g/100g and Fat 26g/100g; these are examples only, never defaults for new products.

The reference layout uses three side-by-side coloured panels:
- Sugar: red.
- Salt: green.
- Fat: orange/yellow.

Each panel contains the English name, Sinhala name, Tamil name and a contrasting value area. Nutrition names are fixed localized labels; no translation API is needed for them. Values must remain editable per product. Allow panel colours, labels, dimensions, order and visibility to be configured in the designer.

## 5. Master food-label template

Create a built-in template based on the user-provided sample image. Preserve its visual hierarchy and order:

1. Product name in English, prominent red heading.
2. Sinhala and Tamil product name line below, red.
3. Product information rows: M.F.D, EXP.D, B.No and N.Wt. Each row has its own independent **Print on Pack** option. If enabled for a row, show **Printed on Pack** in that row instead of its actual value; if disabled, show the actual value.
4. Do not use a single shared “Printed On Pack” control or grouping to govern these four rows.
5. Prominent M.R.P. (Rs.) line.
6. Three nutrition panels: Sugar / Salt / Fat, each with Sinhala and Tamil labels and value per 100g.
7. “Import By:” heading.
8. Importer name and address at the bottom.

Use a white background, thin black rectangular border, black primary text and red product heading, matching the reference as closely as practical. Keep the content editable. Do not replace the reference with an unrelated or generic design. Support user-editable fonts, sizes, colours, spacing, borders, alignment and element positions.

## 6. Custom paper and label layout

No fixed label-size presets or restrictions. Users can enter arbitrary valid dimensions.

Paper settings:
- A4, A5, Letter and Custom paper.
- Custom paper width and height.
- Portrait and landscape orientation.
- Top, bottom, left and right margins.

Label-sheet settings:
- Label width and height.
- Horizontal and vertical gaps.
- Horizontal and vertical label orientation where physically valid.
- Automatic rows and columns.
- Optional manual row/column override, validated against the usable page area.
- Starting label position for partially used sheets.
- Copies per product / print job.

Use millimetres internally for geometry. UI may allow mm and cm with automatic conversion. Validate positive dimensions and non-negative margins/gaps.

For a page width W, left margin L, right margin R, label width w and horizontal gap gx:
columns = floor((W - L - R + gx) / (w + gx)).
For page height H, top margin T, bottom margin B, label height h and vertical gap gy:
rows = floor((H - T - B + gy) / (h + gy)).
Labels per sheet = columns × rows.

The formula is only the geometric page calculation. Also validate against the selected printer's reported printable area, hardware margins and paper capabilities. Never claim a layout is printable solely because it fits the nominal paper dimensions.

## 7. Label designer

- Visual label canvas with zoom and rulers.
- Drag, position, resize and align elements.
- Text, multiline text, images/logos, shapes, borders, barcode and QR code.
- Separate English, Sinhala and Tamil product-name text elements.
- Independent font family, size, weight, alignment and colour.
- Sinhala and Tamil Unicode shaping and font fallback.
- Layer ordering, duplicate, delete, undo and redo.
- Text wrapping and overflow detection.
- Preview at actual physical dimensions.
- Do not silently clip or shrink important text; show an overflow warning and let the user correct it.
- Save the design as a reusable template.
- Provide automatic layout alignment: align text, prices, company details and nutrition panels within the label's usable bounds using consistent margins, spacing and alignment rules. Reflow where appropriate when label dimensions change. Detect and warn about overflow; never silently clip or scale content to hide overflow.

## 8. Template management

Provide built-in templates:
1. Food Product Label — based on the supplied reference; initial default.
2. General Product Label.
3. Barcode & Price Label.

User operations:
- Save as new template and name it.
- Load template.
- Duplicate template.
- Edit and overwrite template.
- Every user-created design must be saveable and reusable as a template, and remain available after the application is closed and reopened.
- Delete user templates.
- Set default template.
- Export and import templates.

A template must store physical label dimensions, element geometry, fonts, colours, visibility, nutrition-panel arrangement and relevant page/layout settings. Built-in templates should be protected from accidental destructive edits; users can save a copy.

## 9. Printing and PDF

Use the Windows printing subsystem so the application can print to any compatible installed Windows printer. Do not assume all printers share the same margins or capabilities.

Required features:
- Printer selection and printer properties.
- Paper size and orientation selection.
- Print preview.
- Test print and calibration.
- Copies and duplicate labels.
- Print selected products.
- Start at a specified label position on a partially used sheet.
- Correct page breaks and multi-sheet output.
- Actual-size / 100% output by default; never silently scale to fit.
- Explicitly warn if printer driver settings may scale or clip output.
- PDF export with correct page and label dimensions for plans whose RSS Core entitlement includes PDF export. Free users must not have PDF export.
- Preserve Sinhala/Tamil glyph shaping and font appearance in PDF output; embed fonts where licensing permits or use a verified equivalent rendering approach.

## 10. Product management and bulk operations

Product management:
- Create, view, edit, duplicate, search and delete products.
- Validate required fields.
- Product history / timestamps.
- Reuse product records with different templates.

Bulk import:
- Excel and CSV import, subject to plan entitlements. Free-tier import restrictions must be explicitly defined in the plan matrix before release.
- Column-to-field mapping.
- Preview and validation before import.
- Per-product copy quantities.
- Report invalid rows; do not silently discard data.

## 11. Translation integration

Define a provider-neutral translation interface. Do not hard-code the application to one vendor. The provider must support English-to-Sinhala and English-to-Tamil. Provider credentials must not be committed to the repository or embedded as public secrets in the desktop executable. Store user-configured credentials securely using Windows-protected credential storage where applicable.

Translation behaviour:
- User explicitly requests translation or accepts a translation prompt.
- Request Sinhala and Tamil product-name suggestions only.
- Cache by source text, source language, target language and provider/version where practical.
- Allow editing and manual entry.
- Preserve manual edits; re-translation requires confirmation before replacing edited values.
- Gracefully handle network errors, rate limits and provider outages.
- Offline mode must retain existing translations and all other application functionality.
- Do not translate dates, batch numbers, weights, prices, ingredients, warnings, importer details or nutrition values.

## 12. Technical architecture

Proposed solution projects:
- RSS.LabelMaker.App — WPF views, view models, resources and application composition.
- RSS.LabelMaker.Core — domain models, interfaces, validation and shared types.
- RSS.LabelMaker.Services — product, translation, template and settings services.
- RSS.LabelMaker.Data — SQLite, Entity Framework Core, repositories and migrations.
- RSS.LabelMaker.Designer — canvas and design document model.
- RSS.LabelMaker.Layout — physical geometry and sheet calculation.
- RSS.LabelMaker.Printing — Windows print integration and print rendering.
- RSS.LabelMaker.Barcodes — barcode and QR generation.
- RSS.LabelMaker.ImportExport — Excel, CSV and PDF workflows.
- Tests — unit, layout, translation and integration tests.

Use dependency injection and clear module boundaries. Keep the layout calculation engine independent of WPF so it can be tested without a UI. Use decimal or carefully controlled double precision for physical geometry and rounding only at rendering boundaries.

## 13. Local database

SQLite entities/tables:
- Products.
- CompanyProfiles (Import By and Distributed By master records, with name and address).
- ProductCompanyAssignments (selected company records and optional product-level overrides).
- ProductNames / localized names.
- TranslationCache.
- NutritionValues (or explicit nutrition properties on product model).
- LabelTemplates.
- TemplateElements.
- PaperProfiles.
- PrinterProfiles.
- PrintJobs.
- ApplicationSettings.

Requirements:
- Automatic database initialization and versioned migrations.
- Transaction-safe writes.
- Backup and restore.
- Safe migration/backup strategy.
- RSS Core owns user authentication and account/session administration. Local SQLite stores product, label, template and print data; it does not store account passwords or become the user database.

## 14. UX and branding

- RSS Label Maker branding by Razeen Secure Solution.
- Professional Windows desktop UI.
- Black-and-gold RSS identity with a clean light workspace; optional dark appearance.
- English application UI initially.
- Sinhala and Tamil support in label content.
- Main navigation: Dashboard, Products, Label Designer, Templates, Import By & Distributed By, Print Manager, Import/Export and Settings.
- Clear validation, print and translation status messages.

## 15. Non-functional requirements

- After initial RSS Core authorization, product creation, editing, design and preview may work offline within the signed entitlement and cached quota rules.
- Printing while offline is permitted only within the cached signed quota and offline grace period.
- Registration, verification, license activation, quota reconciliation, shared-user changes and Google Drive backup/restore require internet.
- Avoid data loss; autosave drafts or provide clear save state.
- No silent text clipping, label overlap or page scaling.
- Stable operation with large product lists and bulk jobs.
- Logging for diagnostics without recording translation credentials or sensitive secrets.
- Installer and application must support the declared Windows 10/11 target; validate actual OS/runtime requirements during build.
- Free use requires a verified RSS Core account and is subject to the Free entitlement's daily print quota and feature flags. Paid access is a one-time annual RSS Premium license entitlement; do not assume recurring monthly billing.

## Logo watermark

- The label designer must allow the user to browse for and load a logo/image file as a watermark on the label.
- Support common image formats used for logos: PNG, JPG/JPEG, BMP, GIF and TIFF.
- Provide a continuously adjustable opacity control from 0% (fully transparent) to 100% (fully opaque), with the selected percentage displayed.
- Show the watermark in the live label preview behind the label text and nutrition panels so it does not obscure product information.
- Provide a remove/clear watermark action.
- Persist the selected image asset/reference and opacity with each saved label template; do not rely on a temporary file path that may later disappear.
- Apply the same watermark, positioning, sizing and opacity in preview, PDF/export and physical printer output.
- Validate missing or unsupported image files gracefully and never crash the designer when an image cannot be loaded.

## 16. Testing and acceptance criteria

1. Create and edit a product with English, Sinhala and Tamil names.
1a. Verify the four independent Print on Pack checkboxes for M.F.D, EXP.D, Batch Number and Net Weight: each checked field alone displays “Printed on Pack” instead of its value, while unchecked fields retain their values. Verify the behaviour in preview, saved/reopened designs, PDF and physical print.
2. Save Import By and Distributed By company/person records in their separate tab; assign them to a product and verify name/address auto-population.
3. Set Pieces per Box; verify the Per-Piece Price checkbox hides/shows the field and label output.
4. Verify automatic per-piece calculation equals Box Price ÷ Pieces per Box, and manual mode preserves the manually entered value.
2. Request translation online; verify both target-language results are cached.
3. Edit Sinhala/Tamil manually; verify re-translation does not overwrite without confirmation.
4. Disconnect internet; verify product management, cached names, templates, layout and printing remain usable.
5. Save, duplicate, edit, export and import a reusable design template; close and reopen the app and verify it remains saved.
6. Change label dimensions and verify automatic alignment/reflow plus visible overflow warnings.
6. Reproduce the reference food label with all required sections and three nutrition panels.
7. Enter arbitrary label dimensions and verify calculated rows, columns and sheets.
8. Validate printer hardware margins and reject or warn on unprintable layouts.
9. Print a test page and measure physical output against configured dimensions.
10. Verify 100% scale and correct partially-used-sheet starting position.
11. Verify Sinhala and Tamil shaping in preview, print and PDF.
12. Import Excel/CSV with field mapping and row-level validation.
13. Back up and restore the SQLite database successfully.
14. Build and launch the Windows installer on supported Windows 10 and Windows 11 test machines.

## 17. Delivery milestones

M1 — Solution foundation, WPF shell, dependency injection, SQLite and migrations.  
M2 — Product management, nutrition fields and translation provider/cache.  
M3 — Reference food-label template and template persistence.  
M4 — Custom dimensions, A4 layout engine and preview.  
M5 — Designer editing tools and barcode/QR.  
M6 — Windows printing, calibration and PDF.  
M7 — Excel/CSV bulk import.  
M8 — RSS Core identity, license-key activation, quota enforcement, device licensing and shared-user access.\nM9 — Google Drive encrypted backup and restore.\nM10 — QA, installer, documentation and release.
M9 — QA, installer, documentation and release.

Do not mark a milestone complete without code, build/test evidence and functional verification. Do not deploy web services; this is a Windows desktop application.

## 18. Repository workflow

- Keep source code, tests, documentation and installer scripts in this repository.
- Inspect existing repository contents before changing files.
- Consolidate related changes before committing.
- Present a change summary before each commit.
- Do not commit blindly.
- Do not claim build, printer or translation verification without actual test evidence.
- No deployment without explicit user approval. User approval to implement authentication does not itself authorize production deployment or DNS changes.
