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

## 4. Units and measurement preferences

- Provide a user-selectable measurement unit for label and custom paper dimensions: millimetres (mm), centimetres (cm), and inches (in).
- Display and accept values in the selected unit throughout label setup and design properties.
- Store/compute physical dimensions internally in a single canonical unit (millimetres) to avoid rounding drift; convert only for display/input.
- Allow changing units without changing the physical size of an existing label or paper.
- Nutrition quantity units are independently selectable per nutrition item. Provide the requested units: g, mg, and L. The chosen unit must appear consistently in the label preview, saved design, PDF and print output.
- Keep numeric value and unit as separate data fields. A numeric value of zero is a valid entered value, not an empty field.

## 5. New-label setup sequence

Before creating the sheet preview, the user must set:

1. Label width and height.
2. Paper size (A4, A5, Letter, and custom dimensions).
3. Paper orientation (portrait or landscape).
4. Target document/print resolution (DPI).
5. Sheet margins and horizontal/vertical label gaps.

- Include common DPI presets (300, 600, 720), but also allow the user to type a custom positive integer DPI manually.
- Validate manually entered DPI and show a clear validation error for zero, negative, non-integer or unsupported values. Do not silently replace a custom value with 300.
- Margins must be individually customizable for top, bottom, left and right. Horizontal and vertical gaps must also be customizable.
- Provide printer-derived hardware printable-area information when available. Distinguish hardware-unprintable margins from user-configured sheet margins so the effective printable area is clear.
- If printer information is unavailable, clearly identify the assumed printable area and allow manual configuration.
- Label size remains editable while in Label Design. Changing label width or height immediately updates the design canvas physical bounds, sheet preview, and labels-per-page calculation. Warn before destructive clipping/reflow; do not silently distort existing elements.
- Recalculate automatically whenever label size, paper, orientation, unit, resolution, printer, margins or gaps change.

## 6. Maximum label count per page

The goal is to print the **greatest possible number of copies of the selected label on one sheet**. This does not mean maximizing, enlarging, stretching, or scaling the label to fill the page.

- Keep each label at its user-defined physical width and height.
- Never automatically enlarge or shrink a label to fill unused page space.
- Calculate how many whole labels fit within the effective printable area, respecting the selected label size, paper size, orientation, margins and gaps.
- Use the available page area efficiently; do not leave a row or column unused when another complete label would fit.

For a given orientation and placement:
- columns = floor((usable width + horizontal gap) / (label width + horizontal gap))
- rows = floor((usable height + vertical gap) / (label height + vertical gap))
- labels per sheet = columns × rows

Evaluate both page orientations when allowed by the user and printer. Choose the valid arrangement with the highest count of complete labels. Do not rotate the label artwork automatically if that would change its intended orientation.

- Show calculated rows, columns, total labels per sheet, remaining space and unused area.
- Recalculate live after relevant setting changes.
- Never overlap or clip labels. If no complete label fits, explain why and show the required printable area.
- Print preview must show each repeated label at the exact user-defined physical size and position.
## 7. Nutrition visibility and units

Sugar, Salt and Fat are independent optional elements:
- Empty/unentered field means its corresponding panel is hidden.
- Any explicitly entered numeric value, including **0**, means its corresponding panel is shown (for example, 0 g must display the panel).
- If all three are empty/unentered, hide the complete nutrition section and reclaim its layout space.
- If only one or two are populated, show only those panels and distribute them evenly.
- Each nutrition item has its own selectable unit (g, mg, L); render the value and selected unit together.
- Do not treat zero as empty, and do not print blank panels, placeholder values, or empty nutrition headings.

## 8. Products and SKU management

The Products page must provide a proper saved-product list view with search/filter and row actions:
- Create and save products.
- Edit an existing product and persist changes to the same product record.
- Delete a product with a clear confirmation.
- Duplicate a product into a new record without overwriting the original.
- Automatically generate a unique SKU when a product is created. SKU generation must be collision-safe and persist the SKU in SQLite. The user may view/copy the SKU; do not regenerate it when editing an existing product.
- Ensure duplicate-product workflow generates a new SKU for the duplicated record.
- Preserve existing product records during schema migration; never discard or reset the existing SQLite database.

## 9. Excel import and export

- Export saved products to an Excel spreadsheet (.xlsx), including SKU and all supported product fields.
- Import products from an Excel spreadsheet (.xlsx) using a clear column/header mapping and a preview before writing to the database.
- Validate required fields and data types; report invalid rows with row numbers and reasons.
- Detect duplicate SKUs and provide an explicit safe resolution (skip, update matching product, or import as a new product with a newly generated SKU). Never silently overwrite records.
- Show import summary (added, updated, skipped, failed) and preserve the original workbook.
- Excel import/export must operate on product data, not flatten or lose multilingual text.

## 10. Live preview and design document

The label preview must render the same document model used by print/PDF output. It must not be a static mock-up assembled from fixed text blocks. Support selecting an element and editing its properties; preserve element positions and style when saving and reopening templates. Canvas display zoom must not alter physical output dimensions.

## 11. Export and print

- Dedicated print preview for the complete paper sheet, including all calculated labels.
- Actual-size print output (100% scale) with explicit user-selected fit-to-page only if offered as a separate mode.
- A4/A5/custom sheet preview with automatically maximized repeated labels.
- PDF export must use the same layout engine as printing.
- Excel export/import for product data as specified above.
- Printer selection must include Windows-installed USB and network printers.
- Respect target DPI in rasterized content and preserve vector text/shapes where possible.
- Provide printer calibration and a test print workflow.

## 12. Preserve existing functionality

Do not regress:
- SQLite product persistence and existing data.
- Account registration/sign-in and RSS Core licensing.
- 14-day Premium trial and paid licence options; no permanent Free licence.
- Automatic backup/sync and restore.
- Automatic updates and session recovery.
- English-to-Sinhala/Tamil automatic transliteration entitlement: five free uses, then Premium; manual local-language entry remains free and offline.
- Existing installer/update flow.

## 13. Acceptance criteria

The redesign is not complete until:
- The main Dashboard visually follows the supplied screenshot's composition and hierarchy.
- Users can enter all product/company details from Dashboard tabs without navigating away.
- Every input updates the live preview.
- Units can be changed without changing the underlying physical dimensions.
- Users can customize all four sheet margins and horizontal/vertical gaps.
- Users can type a custom DPI value as well as select the 300/600/720 presets.
- Label size can be changed during design and the canvas and print calculations update immediately.
- The sheet layout calculates the greatest number of complete copies of the fixed-size label that fit on each page, without automatically enlarging or shrinking the label.
- Print preview displays the complete calculated page accurately.
- Zero-valued nutrition fields remain visible; unentered fields are hidden; all-unentered hides the nutrition section.
- Nutrition units are independently selectable and saved.
- Saved products can be edited, deleted and duplicated.
- Every newly created or duplicated product receives a unique automatically generated SKU.
- Product Excel import/export works with preview, validation and duplicate handling.
- Saved designs reopen correctly.
- PDF and printer output match the same physical layout at actual size.
- Existing account, database, backup, licensing and update functions continue to work.
- A Windows x64 build and relevant functional tests pass before any release is called successful.
