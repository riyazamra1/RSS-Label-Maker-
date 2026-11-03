# RSS Label Maker — Product Requirements Specification

**Product:** RSS Label Maker  
**Publisher:** Razeen Secure Solution  
**Repository:** riyazamra1/RSS-Label-Maker-  
**Target OS:** Windows 10 and Windows 11  
**Distribution:** Free standalone desktop application  
**Specification status:** Approved requirements baseline  
**Version target:** 1.0.0

## 1. Product vision

Build a native Windows desktop application for creating, managing, laying out and printing product labels. The primary built-in template must closely reproduce the user-provided Choco Shop food-label reference: white rectangular label, thin black border, red English product heading, Sinhala and Tamil product-name line, product/date information, M.R.P., three coloured nutrition panels (Sugar, Salt and Fat), and importer details at the bottom.

The reference is a layout guide, not a fixed-size template. Users must be able to change label and paper dimensions without redesigning the template.

## 2. Confirmed product decisions

- Native Windows desktop application; not a WebView wrapper.
- C# / .NET 10 LTS / WPF.
- SQLite local database.
- Free software; mandatory account registration and email verification before first use. No subscription, paid activation or RSS Core dependency.
- Any printer installed and usable through the Windows printing subsystem.
- Normal product management, design, preview and printing must work offline after the device has completed first-time online email verification.
- Online translation is used only to generate Sinhala and Tamil product-name suggestions from an English product name. Translation results are cached locally.
- No automatic translation of any other field.
- No deployment requirement; deliver a Windows installer after validation.

## 2A. Account registration, email verification and offline access — mandatory

### First-run access gate
- On first launch, show a dedicated RSS Label Maker account screen. The user must register and verify the email address before entering the main application.
- Registration fields: full name, email address, password and password confirmation. Validate required fields, email syntax and password policy before sending.
- Registration creates a pending account; it must not grant application access until the email address is verified.
- Send a short-lived, single-use email verification code through the server-side Resend integration. Never put the Resend API key or other backend secrets in the desktop executable.
- Verification screen accepts the code, supports resend with cooldown, shows expiry/errors clearly and prevents brute-force guessing with server-side attempt limits and rate limits.
- After successful verification, issue a device session and persist the verified-device credential securely using Windows DPAPI / Windows Credential Manager. Do not store plaintext passwords or reusable bearer tokens in SQLite, logs or ordinary settings files.
- On subsequent launches, restore the remembered verified-device state without forcing registration again. Provide explicit Sign in, Sign out and Forgot password flows.
- Password reset must use a time-limited, single-use email code or secure reset link; changing a password revokes other active sessions.
- Sign out removes local device credentials and local offline trust. The user must authenticate again to access the application.
- Do not expose whether an email is registered in public responses; use generic account recovery and registration responses where appropriate.

### Offline operation and account state
- Internet is required for initial registration, email verification, first device authorization, sign-in and password recovery.
- After successful verification, the authorized PC must be able to use product management, saved templates, label design, preview, and printing while offline.
- Keep a locally protected verified-device credential / offline authorization record. Validate it locally at startup; synchronize revocation and account status whenever connectivity returns.
- Explain the security boundary: an offline PC cannot learn about a server-side account suspension or remote revocation until it reconnects. Do not claim instant offline revocation.
- Authentication outages must not corrupt or block access to already-authorized offline work. A new/unverified device must remain blocked until verification succeeds.
- Authentication service must never store or receive product records, label content, watermark images, or print jobs.

### Authentication service architecture
- Separate Cloudflare Worker service with D1 for account, verification, device-session and password-reset metadata.
- Resend transactional email API called only from the Worker using a server-side secret. Use an authenticated sender domain configured and verified in Resend before production email delivery.
- Keep this service independent from RSS Core and from the local SQLite database.
- Define versioned API contracts and migrations. Minimum operations: register, verify email code, resend verification, sign in, refresh/check device session, sign out/revoke device, request password reset, complete password reset, and health.
- Store passwords using a modern salted, deliberately expensive password-hashing scheme supported by the Worker runtime; use constant-time comparisons for secret/code verification. Never store plaintext passwords or verification codes.
- Verification codes: cryptographically random, short expiry, one-time use, bounded attempts, resend cooldown, and server-side rate limits for IP/email/device. Hash codes at rest.
- Use HTTPS only, strict input validation, generic auth errors, safe CORS policy, request-size limits, structured security logging without passwords/codes/tokens, and protection against enumeration, replay and brute-force attempts.
- Keep API base URL configurable for development and production; do not hardcode an unverified domain or deploy automatically.
- Document required Cloudflare resources, D1 migrations, Worker secrets, Resend sender-domain setup and Windows client configuration.

### Registration acceptance criteria
1. A fresh installation cannot open the main app before successful registration and email verification.
2. A pending/unverified account cannot enter the app even if it restarts.
3. Correct unexpired verification code activates the account; expired, reused or incorrect codes are rejected.
4. Resend cooldown and attempt/rate limits are enforced server-side.
5. A verified device is remembered securely and can launch offline to use local products, templates, design and printing.
6. Sign out clears local trust; a later launch requires sign-in and online authorization.
7. Password reset works through email and invalidates prior sessions as specified.
8. Auth service outage does not prevent an already-authorized device from accessing local offline work.
9. No product/label data or secrets are transmitted to the authentication service or embedded in the desktop binary.
10. Worker/API tests cover registration, verification, expiration, replay, throttling, password hashing, session lifecycle and recovery.

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
- Maintain Import By and Distributed By records in a dedicated **Import By & Distributed By** tab, with name and full address fields. Products can be assigned saved records; selecting a record automatically fills its name and address on the product. Allow product-level overrides without changing the saved master record.
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
- PDF export with correct page and label dimensions.
- Preserve Sinhala/Tamil glyph shaping and font appearance in PDF output; embed fonts where licensing permits or use a verified equivalent rendering approach.

## 10. Product management and bulk operations

Product management:
- Create, view, edit, duplicate, search and delete products.
- Validate required fields.
- Product history / timestamps.
- Reuse product records with different templates.

Bulk import:
- Excel and CSV import.
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
- A small online authentication service is required only for registration, email verification, sign-in, password recovery and account/session administration. Product, label, template and print data remain local in SQLite; no product data is uploaded for authentication.

## 14. UX and branding

- RSS Label Maker branding by Razeen Secure Solution.
- Professional Windows desktop UI.
- Black-and-gold RSS identity with a clean light workspace; optional dark appearance.
- English application UI initially.
- Sinhala and Tamil support in label content.
- Main navigation: Dashboard, Products, Label Designer, Templates, Import By & Distributed By, Print Manager, Import/Export and Settings.
- Clear validation, print and translation status messages.

## 15. Non-functional requirements

- After initial device verification, product creation, editing, design, preview and printing work without internet.
- Translation is the only core workflow requiring internet.
- Avoid data loss; autosave drafts or provide clear save state.
- No silent text clipping, label overlap or page scaling.
- Stable operation with large product lists and bulk jobs.
- Logging for diagnostics without recording translation credentials or sensitive secrets.
- Installer and application must support the declared Windows 10/11 target; validate actual OS/runtime requirements during build.
- Free use with mandatory email-verified account registration; no subscription, paid activation or RSS Core connection. Online authentication is required for first-time verification and account recovery, not for normal offline label work on an already-authorized PC.

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
M8 — Account registration, email verification, secure device authorization and offline session handling.
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
