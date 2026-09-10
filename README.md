# StoreManagement

Internal web application for LEAD Logistics Innovations, Inc., covering store
record management and a Quality Assurance CAPA (Corrective Action Report)
workflow. Built as an ASP.NET MVC 5 application on .NET Framework 4.8.

## Tech stack

- ASP.NET MVC 5 / Razor views, .NET Framework 4.8
- ADO.NET (`SqlConnection` / `SqlCommand` / `SqlDataReader`) for all data
  access — no Entity Framework or other ORM in active use, despite the
  package reference
- SQL Server (connection string configured in `Web.config`)
- Bootstrap 5, jQuery, SweetAlert2, and SheetJS (`xlsx`) loaded via CDN in
  individual views
- Forms authentication (`Web.config` -> `system.web/authentication`)

## Prerequisites

- Visual Studio 2022 (or later) with the ASP.NET and web development
  workload
- .NET Framework 4.8 developer pack
- SQL Server access (host, credentials, and database name — see
  Configuration below)
- IIS Express (bundled with Visual Studio) for local debugging

## Getting started

1. Clone the repository.
2. Copy `StoreManagement/Web.config.example` to `StoreManagement/Web.config`
   and fill in the real values (see Configuration below). `Web.config` is
   gitignored on purpose — it holds a live database connection string and
   must never be committed.
3. Restore NuGet packages (Visual Studio does this automatically on build,
   or right-click the solution -> Restore NuGet Packages).
4. Open `StoreManagement.sln` (not `StoreManagement.slnx` — see Known
   issues below) and set **StoreManagement** as the startup project.
5. In the toolbar's debug target dropdown (next to the Start button),
   select **IIS Express**, then run.

## Configuration

All configuration lives in `StoreManagement/Web.config`:

- `connectionStrings/StoreContext` — SQL Server connection string used by
  every repository/service class. Update host, catalog, and credentials
  for your environment.
- `appSettings/ApiBaseUrl` — base URL of an external API used for a small
  number of CAPA notification calls (see below). Not required for most of
  the app to function.
- `httpRuntime/maxRequestLength` and
  `system.webServer/security/requestFiltering/requestLimits/maxAllowedContentLength`
  — both raised above their ASP.NET/IIS defaults to allow CAPA attachment
  uploads (photos/documents embedded as base64 in a single JSON POST body).
  If either is lowered, large attachment uploads will fail before reaching
  application code.

## Project structure

- `Controllers/` — one controller per feature area (`StoreController`,
  `CapaController`, `AccountController`, `ItemController`, etc.)
- `Models/` — view models and a handful of ADO.NET repository classes for
  the Store module (`StoreRepository`, `AccountRepository`, etc.)
- `App_Data/` — ADO.NET service classes for the CAPA module and shared
  lookups (`CapaService`, `ItemService`, `VendorService`,
  `WarehouseService`, `UOMService`, and others), plus `CapaAttachments/`,
  the on-disk store for uploaded CAPA photos/documents
- `Views/` — Razor views, organized by controller
- `Scripts/` — third-party JS libraries plus `capa-form.js`, the shared
  client-side logic for the CAPA filing form

## Key modules

### Store management

Store records (name, number, city, coordinates) with search/filter and a
history log (`tbl_StoreLog`) of city/coordinate changes.

### CAPA (Corrective Action Report)

The larger of the two modules. Covers the full lifecycle of a quality
non-conformance report:

1. **Filing** (`Quality.cshtml`) — internal staff file a new CAR, selecting
   a non-conformance type, CAR classification (LLII / Supplier / Trucker /
   DC Site / Other), affected item(s), and defect details. For
   non-conformance types other than "Product Quality & Food Safety" (i.e.
   Environment/Health/Safety/Security, Internal/External Quality Audit,
   Customer Audit), the item-specific fields are optional, since those
   findings don't always tie to a specific physical item.
2. **Response** (`CapaResponse.cshtml`) — the assigned respondent, reached
   through a token-based link (no login required), submits the
   corrective/preventive action taken.
3. **Verification** (`CapaVerification.cshtml`) — QA reviews the response,
   records any stock adjustment, and marks the CAR **Effective** (closes
   it) or **Not Effective**. A "Not Effective" verdict resets the CAR back
   to Awaiting Response (clearing the prior response and verification) and
   increments a loop counter (`tbl_QualityIncidentHdr.loopNumber`), shown
   as a badge on the CAR list.
4. **Status** (`CapaStatus.cshtml`) — a shareable, token-based read-only
   status page for a single CAR, with a PDF export.
5. **List** (`CapaList.cshtml`) — the main CAR list for QA staff, with
   status filters, search, sortable columns, an expandable row per CAR for
   line items beyond the first, and an Excel export (one row per item).

An external API (`ApiBaseUrl` in `Web.config`) is called at two points to
trigger email notifications: `api/capa/insert` when a new CAR is filed, and
`api/capa/respond-again` when a CAR is reset after a "Not Effective"
verification. All actual database writes for the CAPA module happen
locally via ADO.NET — the external API is notification-only.

## Known issues / things to be aware of

- **Use `StoreManagement.sln`, not `StoreManagement.slnx`.** The newer
  `.slnx` format does not reliably resolve this project's IIS Express
  debug target in some Visual Studio versions, causing Visual Studio to
  try to launch `bin/StoreManagement.dll` directly (which fails with "not
  a valid Win32 application" — a DLL isn't a standalone executable). The
  classic `.sln` does not have this problem.
- **Mixed namespaces.** Controllers and models are split across two root
  namespaces — `IsseERP.*` (CAPA-related controllers/services/models) and
  `StoreManagement.*` (Store module). Both coexist in the same project;
  this is intentional carryover from how the modules were built, not a
  bug, but worth knowing when adding `using` directives.
- **NOT NULL columns in `tbl_QualityIncidentHdr`.** Several columns on this
  table (dates, verifier IDs, status, etc.) are `NOT NULL` with no
  default. ADO.NET code that "clears" a field writes a sentinel value
  instead of a real `NULL` — empty string for text columns, `1900-01-01`
  for dates, `0` for FK/id columns. Writing an actual `NULL` to any of
  these throws a constraint-violation exception. Follow the existing
  pattern in `CapaService.cs` (`SubmitVerification`,
  `SubmitNotEffectiveLoop`) when adding new fields to that table.
- **No ORM in active use.** Despite the `EntityFramework` package
  reference, all data access is hand-written ADO.NET. New features should
  follow that same pattern rather than introducing EF for just one area.
