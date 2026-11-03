# RSS Label Maker — Product Requirements Specification

**Product:** RSS Label Maker  
**Publisher:** Razeen Secure Solution  
**Repository:** riyazamra1/RSS-Label-Maker-  
**Target OS:** Windows 10 and Windows 11  
**Distribution:** 14-day Premium trial + RSS Core-managed annual/lifetime licenses  
**Specification status:** Approved requirements baseline  
**Version target:** 1.0.0

## RSS ecosystem placement and distribution
- RSS Label Maker (RLM) is an official product in the RSS ecosystem. It must be managed through the existing RSS Core Portal and presented on the existing public website at https://rssapps.net.
- Do not create a separate RLM portal, standalone account database, separate licensing authority, or separate public website/domain.
- RSS Core Portal: provide the authorized product administration and customer account workflows for RLM, including product configuration, user registration and email verification, the central user database, 14-day trial eligibility/status, license plans and administrator-configurable prices, license-key issuance/redemption, annual/lifetime entitlements, Payments.lk payment fulfillment, device activations, network shared-user seats, and audit history.
- rssapps.net: provide a public RLM product page with product description, features, screenshots, system requirements, proposed pricing, trial terms, and clear registration/purchase calls to action. Registration and purchase must route into the existing RSS Core identity, licensing and payment workflows; do not duplicate accounts or payment records.
- Keep RLM product branding and customer-facing information consistent between RSS Core Portal and rssapps.net. RSS Core remains the source of truth for all user, license, trial, pricing and entitlement data.
- The native Windows RLM application consumes RSS Core APIs for identity and entitlements. The public website must not expose administrative APIs, secrets, internal service URLs or private customer data.

## 1. Product vision

Build a native Windows desktop application for creating, managing, laying out and printing product labels. The primary built-in template must closely reproduce the user-provided Choco Shop food-label reference: white rectangular label, thin black border, red English product heading, Sinhala and Tamil product-name line, product/date information, M.R.P., three coloured nutrition panels (Sugar, Salt and Fat), and importer details at the bottom.

The reference is a layout guide, not a fixed-size template. Users must be able to change label and paper dimensions without redesigning the template.

## 2. Confirmed product decisions

- Native Windows desktop application; not a WebView wrapper.
- C# / .NET 10 LTS / WPF.
- SQLite local database.
- Mandatory account registration and email verification through RSS Core before first use.
- RSS Core is the central user database, identity provider, license authority and subscription/entitlement authority. Do not create a separate RSS Label Maker user database or standalone authentication backend.
- A verified account receives one **14-day Premium trial** with an RSS TRIAL watermark on every printed/PDF page. There is no permanent Free plan. After trial expiry, continued printing and Premium features require an active paid license validated by RSS Core.
- License entitlements govern license term, device count, network user seats, feature access, trial watermark state, cloud backup and restore.
- Any printer installed and usable through the Windows printing subsystem.
- Local label work is stored in SQLite. Offline access is governed by a cached, signed, time-bounded RSS Core entitlement; usage limits and revocations must reconcile with RSS Core when online.
- Online translation is used only to generate Sinhala and Tamil product-name suggestions from an English product name. Translation results are cached locally.
- No automatic translation of any other field.
- No deployment requirement; deliver a Windows installer after validation.

## 2A. RSS Core accounts, licensing and device authorization — mandatory

### Central account database and identity
- The user must register and verify their email before entering RSS Label Maker.
- All account/profile details must be stored in the existing RSS Core user database. RSS Label Maker must not maintain a separate user/account database.
- RSS Core is the source of truth for account identity, email verification, account status, trial eligibility/expiry, license keys, subscription entitlements, device activations, shared-user membership and audit events.
- The desktop app communicates with RSS Core over HTTPS using documented, versioned APIs. Email verification and password recovery emails are sent by RSS Core's existing transactional email mechanism; no separate RSS Label Maker Resend backend or D1 account database.
- Keep product records, label designs, templates, watermark assets and print jobs local by default. Cloud backup content is stored in the user's authorized Google Drive unless a later approved RSS Core storage feature is specified.

### License and subscription model
- A verified RSS Core account receives one 14-day Premium trial, starting at the first successful verified RLM activation. RSS Core enforces trial eligibility centrally; reinstalling or creating another local device profile must not reset it.
- Trial users receive the full Premium feature set for 14 calendar days, including PDF export, import/export workflows, backup/restore where available and Premium print-quality controls. Every printed page and exported PDF page must carry a visible RSS Label Maker TRIAL watermark.
- There is no permanent Free plan and no daily 20-label quota.
- After trial expiry, block new print jobs, PDF export and Premium-only operations until a paid license is activated. Preserve all local products, templates, designs and settings. Never silently convert the account to a permanent Free plan or delete customer data.
- Paid access is activated by entering a license key in the app. RSS Core validates and redeems the key; the desktop app must never decide that a key is valid based only on local string format.
- RSS Core returns a signed entitlement describing product/app, account, plan, status, license model (trial, annual or lifetime), validity period, allowed device activations, allowed network-user seats, feature flags, trial watermark state and cloud-backup permissions.
- RSS Core owns the Payments module and integrates Payments.lk hosted checkout; no separate RSS Pay app/backend. Payment credentials remain server-side. Entitlements are activated only after a verified, idempotent payment webhook, never from a browser return URL.
- Paid plans support both annual (12-month) and lifetime (one-time) licenses in LKR and USD. No monthly recurring billing or automatic renewal is assumed.
- License-key redemption, annual renewal/replacement, expiry, refund/revocation and device deactivation are handled by RSS Core. Lifetime entitlements do not expire, subject to legitimate refund/revocation and account-security controls.
- Show plan name, license status, trial end or paid expiry/valid-through date, annual/lifetime term, purchase currency and price, active devices, available device slots and network-user seats in the app.

### 14-day trial policy
- The trial starts only after verified RSS Core registration and the first successful RLM activation. RSS Core is authoritative for start/end timestamps.
- Trial duration is exactly 14 calendar days. Any future trial-policy changes must be configured centrally in RSS Core, not hard-coded in the desktop application.
- All Premium features are enabled during the trial. A visible RSS Label Maker TRIAL watermark is mandatory on every printed page and every exported PDF page.
- The system trial watermark is separate from the user's optional logo watermark and cannot be removed, hidden or made transparent during the trial. It must not obscure label content.
- Render the trial watermark consistently in print preview, PDF and physical print output. The watermark must appear on each output page, not just the application screen.
- At expiry, RSS Core marks the trial expired. The app must refresh entitlement before allowing new print/PDF operations. A cached signed entitlement may be used only within its signed validity and configured offline grace; changing the PC clock cannot extend the trial.
- Expired trial users see a clear purchase/license-key activation path. Do not auto-create a Free entitlement, apply a permanent Free quota, delete local data or silently reset the trial.

### Commercial plans and proposed pricing
All prices below are proposals subject to RSS Core administrator configuration. They must not be hard-coded into the desktop app. Annual and lifetime prices are independent catalog values for each currency; USD is not automatically calculated from LKR exchange rates.

| RLM license | Included scope | Annual LKR | Annual USD | Lifetime LKR | Lifetime USD |
|---|---|---:|---:|---:|---:|
| Premium — Single PC | 1 owner, 1 registered PC | 5,000 | 25 | 12,000 | 60 |
| Premium — Shared License | 1 owner, default 3 registered PCs; same owner across devices, cloud sync, no LAN-shared workspace | 9,000 | 50 | 22,000 | 120 |
| Premium — Multiple PC (Network) | Default 5 PCs on a shared local network, shared workspace/product data and multiple authorized users | 18,000 | 100 | 40,000 | 220 |

Additional PC pricing must reconcile with the main bundles:
- Shared License extra registered PC: annual LKR 2,000 / USD 12.50; lifetime LKR 5,000 / USD 30.
- Multiple PC (Network) extra network PC: annual LKR 3,250 / USD 18.75; lifetime LKR 7,000 / USD 40.
- These rates reconcile exactly: Single PC + 2 Shared License PCs equals the 3-PC Shared License bundle; Single PC + 4 Network PCs equals the 5-PC Network bundle. Do not add a separate network surcharge on top of the listed bundle.
- Network user seats and PC slots are separate entitlements. The initial network user-seat count and any additional-user fee remain administrator-configurable; never assume one user equals one PC or invent an additional-user price.
- RSS Core Admin can edit prices, currencies, included PC/user limits, add-on rates, trial policy, discounts, plan availability and feature entitlements. Price changes apply to future purchases only; preserve each existing customer's purchase price and license terms.

### Device identity and license activation
- Generate a stable per-installation device ID and bind it to a privacy-preserving Windows device fingerprint. Do not transmit raw hardware serial numbers or sensitive hardware identifiers.
- RSS Core records each activated device against the user/license and enforces the plan's device limit.
- Premium Single PC: one active PC device slot.
- Premium Shared License: one owner may register multiple PCs under the same owner account, within the purchased device limit. This is an individual multi-device license with cloud synchronization, not a LAN-shared multi-user workspace.
- Premium Multiple PC (Network): multiple PCs connect to a shared local-network workspace and shared product data. RSS Core separately enforces network-PC and authorized-user limits. Do not treat this as the individual Shared License plan.
- Device activation must support viewing device name/ID, last-seen time and deactivation from the account/device management UI.
- Reinstall, hardware change, OS reset and device replacement need a documented recovery/deactivation process; do not silently create unlimited new device slots.
- A device ID alone is not proof of license ownership. Use signed device-bound tokens and server-side activation records.

### Multi-user / network licensing
- The Multiple PC (Network) plan supports a license owner/business inviting additional RSS Core accounts as shared users, subject to the plan's shared-user seat count.
- Shared users authenticate with their own verified RSS Core accounts. Never share one owner's password or license key as a common login.
- RSS Core manages invitations, acceptance, removal, role/permission assignments and seat counts.
- Shared-user access must be scoped to the licensed RSS Label Maker network workspace/business and its permitted data. Define roles such as Owner/Admin, Editor and Print-only; permissions must be enforced by the app and RSS Core APIs where applicable.
- Shared-user seats and PC device activations are separate entitlement counts. Do not treat one PC as one user or one user as one PC.

### Offline policy
- A device with a valid cached signed entitlement may continue permitted local work while offline, within the configured bounded offline grace period and signed entitlement validity.
- New registration, email verification, first activation, license-key redemption, adding a device, inviting network users, cloud backup/restore and license changes require online RSS Core access.
- Offline activity must be recorded locally in a tamper-evident queue and reconciled with RSS Core on reconnection. Detect duplicate/replayed events.
- An offline PC cannot immediately learn of a server-side revocation, seat removal or annual subscription expiry. Do not claim immediate offline revocation.
- When the signed entitlement expires or the offline grace period is exhausted, block restricted printing/Premium operations; preserve access to local data and permit account/license management.
- Do not silently erase or lock customer data when a trial or paid license expires.

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
- All authentication, email verification, trial/paid license validation, device activation, network-seat management and entitlement retrieval must go through RSS Core.
- Desktop client stores only short-lived access credentials and a protected refresh/device credential; never store plaintext passwords or reusable bearer tokens in SQLite, logs or ordinary settings files.
- Use TLS, strict request validation, signed short-lived access tokens, token rotation/revocation, rate limits, generic auth errors and audit logging.
- Do not embed RSS Core secrets, Payments.lk credentials, Google client secrets or signing keys in the Windows binary.
- RSS Core must enforce ownership and authorization for every user/license/device/seat operation. Never trust client-supplied plan names, trial dates, prices, currency, device counts, user seats or feature flags.
- API failures must not corrupt local data. Preserve bounded offline operation for already-authorized devices.

### Acceptance criteria
1. New users register and verify through RSS Core; user details appear in the RSS Core user database.
2. No separate Label Maker account database or standalone auth Worker/D1 is used.
3. RSS Core issues one 14-day Premium trial after verified registration and first RLM activation; no permanent Free plan or daily free-label quota exists.
4. Trial enables Premium features but watermarks every printed/PDF page; after expiry, new printing and Premium operations require a paid license and local data remains intact.
5. RSS Core validates and activates annual or lifetime license keys in LKR or USD, using the plan and price snapshot configured at purchase.
6. Single-PC, Shared License multi-device and Multiple PC (Network) device/user limits are enforced by RSS Core.
7. Network shared users authenticate separately and consume authorized user seats distinct from PC slots.
8. Offline access follows signed entitlement and bounded offline-grace rules; trial expiry cannot be extended by changing the PC clock.
9. Google Drive backup and restore use secure OAuth, encrypted archives and integrity validation, subject to entitlement.
10. Existing local data is preserved across expiry, sign-out, network outage and restore failures.
11. Payment fulfillment is based on verified Payments.lk webhook processing by RSS Core for annual and lifetime purchases.
12. Tests cover trial start/expiry/reinstall resistance, watermark rendering, license redemption, annual/lifetime validity, currency/price snapshots, device replacement, network seats, offline expiry and backup/restore integrity.

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
- Maintain Import By and Distributed By master records in a dedicated **Import By & Distributed By** tab according to RSS Core entitlement flags. Trial users receive Premium access; paid-plan permissions are administrator-configurable.
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
- Premium print quality: selectable high-quality output including 300 DPI and 600 DPI where supported by the selected printer/driver. Query actual printer capabilities and never advertise unsupported resolution. Trial users receive Premium print-quality controls with the mandatory RSS TRIAL page watermark; paid Premium output has no system trial watermark.
- Paper size and orientation selection.
- Print preview.
- Test print and calibration.
- Copies and duplicate labels.
- Print selected products.
- Start at a specified label position on a partially used sheet.
- Correct page breaks and multi-sheet output.
- Actual-size / 100% output by default; never silently scale to fit.
- The system RSS TRIAL watermark is separate from the user's optional logo watermark. Apply it to every printed/PDF page during the 14-day trial, including multi-page jobs.
- Explicitly warn if printer driver settings may scale or clip output.
- PDF export with correct page and label dimensions when enabled by the RSS Core entitlement. Trial PDF pages must carry the mandatory RSS TRIAL watermark; expired trials require a paid entitlement.
- Preserve Sinhala/Tamil glyph shaping and font appearance in PDF output; embed fonts where licensing permits or use a verified equivalent rendering approach.

## 10. Product management and bulk operations

Product management:
- Create, view, edit, duplicate, search and delete products.
- Validate required fields.
- Product history / timestamps.
- Reuse product records with different templates.

Bulk import:
- Excel and CSV import, subject to RSS Core plan entitlements. The trial receives Premium import/export features; paid-plan feature flags are configured by RSS Core Admin.
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

- After initial RSS Core authorization, product creation, editing, design and preview may work offline within the signed entitlement and configured offline grace.
- Printing while offline is permitted only while the cached signed entitlement and offline grace remain valid. Trial watermark rules continue to apply.
- Registration, verification, license activation, entitlement refresh, network-user changes and Google Drive backup/restore require internet.
- Avoid data loss; autosave drafts or provide clear save state.
- No silent text clipping, label overlap or page scaling.
- Stable operation with large product lists and bulk jobs.
- Logging for diagnostics without recording translation credentials or sensitive secrets.
- Installer and application must support the declared Windows 10/11 target; validate actual OS/runtime requirements during build.
- RLM access requires a verified RSS Core account and a valid 14-day Premium trial or paid entitlement. There is no permanent Free plan. Paid licensing supports annual (12-month) and lifetime (one-time) terms in LKR and USD.

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
M8 — RSS Core identity, 14-day trial, license-key activation, annual/lifetime pricing, device licensing and network shared-user access.
M9 — Google Drive encrypted backup and restore.
M10 — QA, installer, documentation and release.
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
