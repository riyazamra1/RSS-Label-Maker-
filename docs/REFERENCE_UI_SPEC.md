# RSS Label Maker — Reference UI and Workflow Specification

Status: implementation specification for `feat/reference-faithful-label-designer`  
Primary visual reference: user-provided RSS Label Maker v1.0.0 screenshot in the project conversation.

## 1. Product direction

Rebuild the Windows desktop application to match the supplied reference screenshot as closely as practical. This is a native WPF desktop application, not a web dashboard. Preserve the existing SQLite database, account/licensing integration, backup/sync, update mechanism, and offline product-entry capabilities.

## 2. Main window layout

- Black and gold RSS branded header using the original, unmodified RSS logo asset.
- Dark vertical left navigation.
- Light blue/off-white main workspace with blue section headers.
- Top action bar: New Product, Edit, Delete, Save Template, Load Template, and template selector.
- Main Dashboard is the production workspace. Product data entry and label preview are visible together.
- Left/main content area contains tabbed product and company data entry.
- Right content area contains a large live label preview.
- Bottom area contains template management and export/print actions.
- Bottom status bar shows readiness, selected product, label dimensions, template, and database connection state.
- Do not substitute a generic welcome/analytics dashboard for the production workspace.

## 3. Dashboard tabs

The Dashboard must contain these tabs and share one underlying label document/product state:

1. **Product Details** — English, Sinhala, Tamil product names; manufacture date; expiry date; batch number; net weight; MRP; pricing and box details.
2. **Import By & Distributed By** — two separate company name/address records, each selectable from saved company records.
3. **Label Design** — element-level editing of text, font family, font size, weight, colour, alignment, position, visibility, and ordering; support moving, resizing, duplicating and deleting elements.
4. **A4 Print Preview** — actual paper sheet preview with repeated labels, margins, gaps, rows, columns and total count.

Changing data in any tab updates the same live label preview immediately.

## 4. New-label setup sequence

Before creating the sheet preview, the user must set:

1. Label width and height in millimetres.
2. Paper size (A4, A5, Letter, and custom dimensions).
3. Paper orientation (portrait or landscape).
4. Target document/print resolution: 300, 600, or 720 DPI.
5. Sheet margins and horizontal/vertical label gaps; allow manual rows/columns as an override.

Calculate the maximum labels per sheet from the usable printable area and label dimensions. Use:
- columns = floor((printable width + horizontal gap) / (label width + horizontal gap))
- rows = floor((printable height + vertical gap) / (label height + vertical gap))
- labels per sheet = columns × rows

Use the selected printer's reported printable area/hardware margins when available. If unavailable, clearly identify the assumed margins and allow the user to configure them. Never silently clip labels. Recalculate when label size, paper, orientation, printer, margins, gaps or layout changes.

## 5. Nutrition visibility

Sugar, Salt and Fat are independent optional elements:
- Empty field means its corresponding panel is hidden.
- Non-empty field means its corresponding panel is shown.
- If all three are empty, hide the complete nutrition section and reclaim its layout space.
- If only one or two are populated, show only those panels and distribute them evenly.
- Do not print blank panels, placeholder values, or empty nutrition headings.

## 6. Live preview and design document

The label preview must render the same document model used by print/PDF output. It must not be a static mock-up assembled from fixed text blocks. Support selecting an element and editing its properties; preserve element positions and style when saving and reopening templates. Canvas display zoom must not alter physical output dimensions.

## 7. Export and print

- Actual-size print output (100% scale) with explicit user-selected fit-to-page only if offered as a separate mode.
- A4/A5/custom sheet preview with calculated repeated labels.
- PDF export must use the same layout engine as printing.
- Excel export for product data.
- Printer selection must include Windows-installed USB and network printers.
- Respect target DPI in rasterized content and preserve vector text/shapes where possible.
- Provide printer calibration and a test print workflow.

## 8. Preserve existing functionality

Do not regress:
- SQLite product persistence and existing data.
- Account registration/sign-in and RSS Core licensing.
- 14-day Premium trial and paid licence options; no permanent Free licence.
- Automatic backup/sync and restore.
- Automatic updates and session recovery.
- English-to-Sinhala/Tamil automatic transliteration entitlement: five free uses, then Premium; manual local-language entry remains free and offline.
- Existing installer/update flow.

## 9. Acceptance criteria

The redesign is not complete until:
- The main Dashboard visually follows the supplied screenshot's composition and hierarchy.
- Users can enter all product/company details from Dashboard tabs without navigating away.
- Every input updates the live preview.
- Users can select, move, resize, format, reorder, duplicate and delete label elements.
- Label dimensions, paper, orientation, resolution, margins and gaps drive accurate sheet calculations.
- The sheet preview shows the correct row/column count and total labels.
- Empty nutrition fields hide their corresponding panels; all-empty hides the nutrition section.
- Saved designs reopen correctly.
- PDF and printer output match the same physical layout at actual size.
- Existing account, database, backup, licensing and update functions continue to work.
- A Windows x64 build and relevant functional tests pass before any release is called successful.
