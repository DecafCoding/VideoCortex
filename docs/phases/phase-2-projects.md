# Phase 2: Projects

The following plan should be complete, but it is important that you validate documentation and codebase patterns and task sanity before you start implementing.

Pay special attention to naming of existing utils, types, and models. Import from the right files.

## Phase Description

This phase gives **Video Cortex** its first real feature: **Projects**. A project is the top-level container the user creates for a research topic, and — uniquely to this product — **each project is a standalone OKF-HTML library on disk**. Creating a project must therefore do two things atomically-in-spirit: persist a `Project` row (built in Phase 1) *and* scaffold a browsable, OKF-conformant library folder under `LibrarySettings.RootPath\<Project Name>\` containing a copy of the shared `theme.css` and an initial empty `index.html` shell with a valid root-index `okf-meta` block.

This phase delivers **Project CRUD** (create / list / detail / delete) with the Blazor UI, the `IOkfLibraryStore` disk-writer service (its `CreateLibraryAsync` method only — later phases add page/report writers), and the slug-generation + collision-handling helpers. It deliberately stops **before** video ingestion: the project detail page renders the project header, an "Open library ↗" link, and a **placeholder** where Phase 3's video table will go. There is no add-video box, no worker, no LLM call here.

This builds directly on Phase 1: the `VideoCortexDbContext`, the `Project`/`Video` entities, `VideoCortexPaths`, the `LibrarySettings` record, and the bundled OKF templates in `wwwroot/okf/` all already exist. The reference implementation for every pattern remains **SkipWatch** at `C:\Repos\SkipWatch\SkipWatch` — read those files to copy conventions (atomic file store, project CRUD service, slug helper, Blazor page shapes), then write clean, minimal Video Cortex equivalents. Do **not** add a project reference to SkipWatch and do **not** copy its channel/topic/triage/quota/wiki-ingest code.

## User Stories

As the single local user
I want to create a project for a research topic and immediately get a real, browsable OKF library folder on disk
So that the durable artifact exists from the moment the project does, even before I add any videos.

As the single local user
I want to list my projects, open a project's detail page, and delete a project I no longer need
So that I can manage my research topics from the app.

As the careful owner of a precious `SecondBrain` library
I want project deletion to remove only my database rows by default and never touch a pre-existing sibling folder like *Wild Flowers*
So that the app can never corrupt or delete knowledge it did not create.

## Problem Statement

Phase 1 produced a runnable app with an empty database and template assets, but there is no way to create anything. There is no project CRUD, no service that writes an OKF library folder to disk, and no slug/collision logic to keep folder and URL names unique and filesystem-safe. Until projects exist and scaffold their on-disk libraries, no later phase (add video, transcript, summary, report) has a container to attach to.

## Solution Statement

Add a `Features/Projects` vertical slice to the host (list page `/projects`, detail page `/projects/{slug}`, an add-project form, and a delete confirmation) backed by a `ProjectService` that does explicit `DbContext` work and returns result-record types (`SaveProjectResult`, `DeleteProjectResult`). Add a `SlugHelper` (mirrored from SkipWatch) for lowercase-hyphenated, collision-resolved slugs. Add an `IOkfLibraryStore` / `OkfLibraryStore` in `.Core` that, on project creation, creates `RootPath\<Project Name>\`, copies the bundled `theme.css` into it, and writes an initial `index.html` from the bundled OKF root-index template with a valid `okf-meta` block (`type: "Index"`, `okf_html_version: "0.1"`, title, description) and a relative `theme.css` link. Wire the store into DI with the templates read via `IWebHostEnvironment.WebRootPath` (the mechanism Phase 1 documented). Deletion removes DB rows by default; on-disk folder deletion is opt-in, off by default, and confined to `RootPath\<Name>\` — the app never enumerates or touches unknown sibling folders.

## Phase Metadata

**Phase Type**: New Capability (first feature slice)
**Estimated Complexity**: Medium
**Primary Systems Affected**: `Features/Projects` (new Blazor slice), `.Core/Services/Library` (new disk-writer service), `.Core/Services/Utilities` (new slug helper), DI composition in `Program.cs`
**Dependencies**: Phase 1 complete — `VideoCortexDbContext`, `Project`/`Video` entities + `ProjectStatus`/`VideoStatus` enums, `VideoCortexPaths`, `LibrarySettings` record, and the bundled OKF templates (`wwwroot/okf/theme.css`, `concept.html`, `index.html`) with placeholders intact. .NET 10 SDK; EF Core 10.

---

## CONTEXT REFERENCES

### Relevant Codebase Files IMPORTANT: YOU MUST READ THESE FILES BEFORE IMPLEMENTING!

These are **SkipWatch reference files** (a separate repo) — read them for patterns to mirror, then write minimal Video Cortex equivalents. Do not reference or depend on the SkipWatch projects.

- `C:\Repos\SkipWatch\SkipWatch\SkipWatch.Core\Services\Wiki\IWikiFileStore.cs` (lines 1–57) — Why: interface shape and XML-doc style for a per-project atomic file store. Video Cortex's `IOkfLibraryStore` mirrors the "one project = one folder, atomic writes, sanitized filenames" contract, but writes **HTML into the library root** rather than Markdown into `pages/`.
- `C:\Repos\SkipWatch\SkipWatch\SkipWatch.Core\Services\Wiki\WikiFileStore.cs` (lines 1–173) — Why: the exact implementation patterns to copy: `UTF8Encoding(encoderShouldEmitUTF8Identifier: false)` (UTF-8 no BOM, line 15), `SafeFileName` regex `^[A-Za-z0-9._-]+$` (line 16), `AtomicWriteAsync` = write `.tmp` then `File.Move(..., overwrite: true)` (lines 152–157), `SanitizeFileName` guard (lines 159–172), and the `ProjectDir` traversal guard rejecting `/`, `\`, `..` (lines 144–150).
- `C:\Repos\SkipWatch\SkipWatch\SkipWatch\Features\Projects\Services\ProjectService.cs` (lines 1–407) — Why: the project CRUD service pattern to mirror: constructor DI of `DbContext` + collaborators; `CreateAsync` (lines 41–126) name-trim/normalize, duplicate check, `SlugHelper.UniqueSlugAsync`, `DbUpdateException` race handling, result records; `DeleteAsync` (lines 309–392) DB-first then best-effort file cleanup with try/catch that never fails the operation; `GetForEditAsync` `AsNoTracking().Select(...)` projection (lines 394–400); `Normalize` helper (lines 402–406). **Simplify**: drop the YouTube/channel/transcript/VideoProjects machinery entirely — Video Cortex projects own videos directly (Phase 3) and have no join table.
- `C:\Repos\SkipWatch\SkipWatch\SkipWatch.Core\Services\Utilities\SlugHelper.cs` (lines 1–55) — Why: copy verbatim in spirit. `ToSlug(input, fallback)` (lowercase, non-alphanumeric runs → single hyphen, trim hyphens, fallback on empty; lines 11–32) and `UniqueSlugAsync(preferred, exists, fallback)` (append `-2`, `-3`, …; GUID escape hatch after 100; lines 38–54).
- `C:\Repos\SkipWatch\SkipWatch\SkipWatch\Features\Projects\Components\Projects.razor` (lines 1–43) — Why: list-page conventions — `@page "/projects"`, load-on-init, empty-state message, card-per-project linking to `/projects/@p.Slug`. (Video Cortex uses a minimal in-app stylesheet, **not** Bootstrap — keep markup plain/semantic; do not copy `card`/`row g-3` Bootstrap classes wholesale.)
- `C:\Repos\SkipWatch\SkipWatch\SkipWatch\Features\Projects\Components\ProjectPage.razor` (lines 1–148) — Why: detail-page conventions — `@page "/projects/{Slug}"`, `[Parameter] public string Slug`, load-by-slug, not-found branch, header + delete-confirm modal pattern. **Simplify**: Video Cortex Phase 2 has no tabs, no wiki/guides/coverage, no edit/rebuild — just header, "Open library ↗" link, a **video-table placeholder**, and a delete confirmation.
- `C:\Repos\SkipWatch\SkipWatch\SkipWatch\Features\Projects\Components\AddProjectForm.razor` (lines 1–102) — Why: `EditForm` + `DataAnnotationsValidator` + form-model + busy-state + `OnProjectAdded` callback pattern for the create form. (Drop the `Goal`/`ProjectGoal` field — not in the Video Cortex data model. Keep Name, Description, AI Instructions.)

### OKF template + spec references (read for output fidelity)

- `C:\Users\JasonUser\Documents\SecondBrain\okf\SPEC.md` — §3 Library structure (theme.css at root, **relative** links only — a leading `/` breaks under `file://`, §3.2), §6 Index files (root index is the front door; lists concepts grouped under headings), §9 Conformance (theme.css present; every non-reserved `.html` has an `okf-meta` that parses as JSON with non-empty `type`; internal links relative), §10 Versioning (`okf_html_version: "0.1"` declared on the **root** `index.html` only).
- `C:\Users\JasonUser\Documents\SecondBrain\okf\templates\index.html` (bundled by Phase 1 at `VideoCortex\wwwroot\okf\index.html`) — the root-index template. Placeholders: `{{LIBRARY_TITLE}}` (×3: `<title>`, meta `title`, `<h1>`), `{{THEME_HREF}}`, `{{LIBRARY_DESCRIPTION}}`, and a group/entry block (`{{GROUP_HEADING}}`, `{{ENTRY_HREF}}`, `{{ENTRY_TITLE}}`, `{{ENTRY_DESCRIPTION}}`). For an **empty** library, fill title/href/description and **remove the sample `<section class="okf-index-group">…</section>` block** (no concepts yet). `okf-meta` carries `type: "Index"`, `title`, `okf_html_version: "0.1"`.
- `C:\Users\JasonUser\Documents\SecondBrain\Wild Flowers\index.html` (lines 1–100) — the hand-built reference to match: `<!doctype html>`, `<link rel="stylesheet" href="theme.css">` (relative, root-level), `okf-meta` block `{ "type": "Index", "title": "Wild Flowers", "okf_html_version": "0.1" }`, `<h1>` + `<p class="description">`. An empty Video Cortex library must look like this **minus the index groups**.
- `C:\Users\JasonUser\Documents\SecondBrain\okf\templates\concept.html` — the concept template (NOT written this phase; referenced only so `IOkfLibraryStore`'s future `WriteConceptPageAsync` extension point is named consistently). Do not implement concept writing here.
- `C:\Users\JasonUser\Documents\SecondBrain\okf\templates\theme.css` (bundled by Phase 1 at `VideoCortex\wwwroot\okf\theme.css`) — copied byte-for-byte into each project. `OkfLibraryStore` reads it from `wwwroot/okf/theme.css`; do not regenerate or reformat.

### New Files to Create

- `C:\Repos\VideoCortex\VideoCortex.Core\Services\Utilities\SlugHelper.cs` — static `ToSlug` + `UniqueSlugAsync` (mirror SkipWatch).
- `C:\Repos\VideoCortex\VideoCortex.Core\Services\Library\IOkfLibraryStore.cs` — interface: `CreateLibraryAsync(Project project, CancellationToken)` this phase; documented future extension points `WriteConceptPageAsync` / `WriteReportAsync` / `DeleteConceptAsync` / `DeleteLibraryAsync` (declared or noted, **not implemented** now — see GOTCHA).
- `C:\Repos\VideoCortex\VideoCortex.Core\Services\Library\OkfLibraryStore.cs` — file-backed impl. Ctor takes the library **root path** (from `LibrarySettings.RootPath`) and the **templates directory** (the `wwwroot/okf` absolute path). Atomic writes, UTF-8 no BOM, folder-name sanitizer, traversal guard.
- `C:\Repos\VideoCortex\VideoCortex.Core\Services\Library\OkfLibraryStoreOptions.cs` (optional) — a small record/class carrying `RootPath` + `TemplatesDir` if you prefer options over ctor strings; otherwise register via a factory lambda in `Program.cs`.
- `C:\Repos\VideoCortex\VideoCortex\Features\Projects\Services\IProjectService.cs` — `CreateAsync`, `DeleteAsync`, `ListAsync`, `GetBySlugAsync`.
- `C:\Repos\VideoCortex\VideoCortex\Features\Projects\Services\ProjectService.cs` — CRUD over `VideoCortexDbContext`; calls `IOkfLibraryStore.CreateLibraryAsync` on create; result records.
- `C:\Repos\VideoCortex\VideoCortex\Features\Projects\Models\ProjectFormModel.cs` — `Name` (required), `Description?`, `AIInstructions?` with data annotations.
- `C:\Repos\VideoCortex\VideoCortex\Features\Projects\Models\Results.cs` — `SaveProjectResult`, `DeleteProjectResult`, `ProjectSummaryDto`, `ProjectDetailDto` records.
- `C:\Repos\VideoCortex\VideoCortex\Features\Projects\Components\Projects.razor` — `/projects` list page.
- `C:\Repos\VideoCortex\VideoCortex\Features\Projects\Components\ProjectDetail.razor` — `/projects/{slug}` detail page (header + open-library link + video-table placeholder + delete-confirm).
- `C:\Repos\VideoCortex\VideoCortex\Features\Projects\Components\AddProjectForm.razor` — create form component.
- `C:\Repos\VideoCortex\VideoCortex\Features\Projects\Components\_Imports.razor` — feature-scoped usings (optional; can fold into root `_Imports.razor`).
- `C:\Repos\VideoCortex\VideoCortex.Tests\Library\OkfLibraryStoreTests.cs` — folder + theme.css + index.html + okf-meta assertions; sibling-folder-untouched safety test.
- `C:\Repos\VideoCortex\VideoCortex.Tests\Projects\ProjectServiceTests.cs` — create/list/delete + slug collision.
- `C:\Repos\VideoCortex\VideoCortex.Tests\Utilities\SlugHelperTests.cs` — slug + uniqueness.

### Files to Modify

- `C:\Repos\VideoCortex\VideoCortex\Program.cs` — register `IProjectService` (scoped) and `IOkfLibraryStore` (singleton, constructed from `LibrarySettings.RootPath` + `env.WebRootPath + "/okf"`); ensure a nav link to `/projects`.
- `C:\Repos\VideoCortex\VideoCortex\Components\Layout\MainLayout.razor` (or nav) — add a "Projects" link.
- `C:\Repos\VideoCortex\VideoCortex\_Imports.razor` — add `@using VideoCortex.Features.Projects.*` namespaces if not feature-scoped.

### Relevant Documentation YOU SHOULD READ THESE BEFORE IMPLEMENTING!

- [Blazor forms & validation (`EditForm`, `DataAnnotationsValidator`)](https://learn.microsoft.com/en-us/aspnet/core/blazor/forms/validation) — Why: the add-project form.
- [Blazor routing & route parameters](https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/routing) — Why: `/projects/{slug}` detail routing and `NavigationManager` redirects after create/delete.
- [`IWebHostEnvironment.WebRootPath`](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/generic-host) — Why: locating the bundled `wwwroot/okf` templates for server-side reads (the mechanism Phase 1 Task 8 documented).
- [`File.Move(source, dest, overwrite)`](https://learn.microsoft.com/en-us/dotnet/api/system.io.file.move) — Why: the atomic temp-file+move write.
- OKF-HTML spec: `C:\Users\JasonUser\Documents\SecondBrain\okf\SPEC.md` — §3, §6, §9, §10 (see above).

### Patterns to Follow

**Naming**: PascalCase types/methods, `I`-prefixed interfaces, records for results/DTOs/form-models, one primary type per file. Namespaces `VideoCortex.Features.Projects.*` (host) and `VideoCortex.Core.Services.Library` / `VideoCortex.Core.Services.Utilities` (core).

**Result records over exceptions for expected outcomes** (mirror SkipWatch): `SaveProjectResult(bool Success, ProjectSummaryDto? Project, string? ErrorMessage, bool IsDuplicate)`; `DeleteProjectResult(bool Success, string? ErrorMessage)`. Reserve exceptions for programmer errors / genuinely unexpected IO.

**DB-first, files-second, best-effort cleanup** (mirror SkipWatch `DeleteAsync`): commit the DB change first; then do disk work in a `try/catch` that logs and swallows so a filesystem hiccup never leaves the DB and UI inconsistent. For **create**, the order is: scaffold the library on disk **then** `SaveChangesAsync` — or save first then scaffold — pick one and document it; the safer choice is **save the row first, then scaffold** so a disk failure leaves a project with no folder (recoverable by re-running create/repair) rather than an orphan folder with no row. Document your choice in NOTES.

**Filesystem safety** (mirror `WikiFileStore`): all writes atomic (temp + move, overwrite), UTF-8 **no BOM**, the destination folder is always `RootPath\<sanitized name>\`, and the store rejects any name containing `/`, `\`, or `..`. The store never enumerates `RootPath` looking at sibling folders and never deletes anything outside a known project folder.

**Folder name vs slug**: the project **folder** name is the human display **Name**, lightly sanitized (the user's existing libraries are `Wild Flowers` / `Sci Fi Writing` — spaces preserved). The **slug** (lowercase-hyphenated, `^[A-Za-z0-9._-]+$`-safe) is only for **URL routing** and **DB uniqueness**, never the folder name. Do not slugify the folder.

**No Bootstrap**: SkipWatch uses Bootstrap classes; Video Cortex uses a minimal in-app stylesheet. Write plain semantic HTML with small utility classes of your own — do not import Bootstrap.

**Warnings-as-errors**: everything compiles clean under `TreatWarningsAsErrors=true`; unused usings and nullable warnings fail the build.

---

## IMPLEMENTATION PLAN

**Rendering**: Milestones — the phase has natural sub-layers (slug helper → disk-writer service → CRUD service → Blazor UI → tests → PR) worth surfacing.

**Rationale**: 9 tasks across four distinct layers where the payoff is integration — creating a project must both persist a row *and* materialize a conformant library on disk. Milestone checkpoints prove that integration (e.g., "creating a project via the UI produces a browsable empty OKF library") beyond each task's isolated VALIDATE.

Tasks execute one at a time, top to bottom. Task numbering is contiguous across milestones.

### Milestone 1: Slug helper & OKF library store

Deterministic slug/collision logic and the disk-writer that scaffolds an OKF library folder exist and are unit-tested.

**Validation checkpoint**: `dotnet test VideoCortex.slnx --filter "FullyQualifiedName~SlugHelper|FullyQualifiedName~OkfLibraryStore"` passes; a scaffolded folder contains `theme.css` + a conformant `index.html`.

#### Task 1: Slug helper

Port the SkipWatch slug utility into `.Core`.

- **IMPLEMENT**: `VideoCortex.Core.Services.Utilities.SlugHelper` — static `string ToSlug(string? input, string fallback)` (lowercase, collapse non-alphanumeric runs to a single `-`, trim leading/trailing `-`, return `fallback` when empty) and `Task<string> UniqueSlugAsync(string preferred, Func<string, Task<bool>> exists, string fallback)` (append `-2`, `-3`, …; after 100 attempts fall back to `{fallback}-{guid:N}` truncated). Result must always match `^[A-Za-z0-9._-]+$`.
- **PATTERN**: `SkipWatch.Core\Services\Utilities\SlugHelper.cs` (lines 1–55) — copy in spirit.
- **IMPORTS**: `System`, `System.Text`.
- **GOTCHA**: `ToSlug` must never emit consecutive hyphens or a trailing hyphen (the `lastWasHyphen` flag handles this). `char.ToLowerInvariant`, not `ToLower`. This helper produces **slugs**, not folder names — do not use it for the on-disk folder.
- **VALIDATE**: create `VideoCortex.Tests/Utilities/SlugHelperTests.cs` asserting `ToSlug("Local LLM Inference","p") == "local-llm-inference"`, `ToSlug("  ...  ","fb") == "fb"`, and `UniqueSlugAsync("x", s => Task.FromResult(s is "x" or "x-2"), "fb")` returns `"x-3"`; then `cd /c/Repos/VideoCortex && dotnet test VideoCortex.slnx --filter "FullyQualifiedName~SlugHelper"` exits 0.

#### Task 2: `IOkfLibraryStore` interface

Define the disk-writer contract with this phase's method plus documented future extension points.

- **IMPLEMENT**: `VideoCortex.Core.Services.Library.IOkfLibraryStore` with `Task CreateLibraryAsync(Project project, CancellationToken ct = default)` — creates `RootPath\<Project.Name sanitized>\`, copies `theme.css`, writes an initial empty root `index.html`. Idempotent: if the folder/files already exist, overwrite the `index.html` shell and (re)copy `theme.css` without throwing. Add XML-doc-only stubs (comments, not method signatures) describing the **future** `WriteConceptPageAsync`, `WriteReportAsync`, `DeleteConceptAsync`, and `DeleteLibraryAsync` methods that Phases 5/6/deletion will add — so the interface's growth path is legible.
- **PATTERN**: `SkipWatch.Core\Services\Wiki\IWikiFileStore.cs` (lines 1–57) — XML-doc density and the "idempotent ensure" verb style.
- **IMPORTS**: `VideoCortex.Core.Entities` (for `Project`), `System.Threading`, `System.Threading.Tasks`.
- **GOTCHA**: Do **not** declare `WriteConceptPageAsync` etc. as real interface members this phase — an unimplemented member breaks the build, and stubbing them forces premature design. Document them in `<remarks>` / comments only.
- **VALIDATE**: `cd /c/Repos/VideoCortex && dotnet build VideoCortex.Core` exits 0.

#### Task 3: `OkfLibraryStore` implementation

Implement the file-backed store that scaffolds a conformant, browsable empty OKF library.

- **IMPLEMENT**: `VideoCortex.Core.Services.Library.OkfLibraryStore : IOkfLibraryStore`. Constructor takes `string rootPath` (library root, e.g. `LibrarySettings.RootPath`) and `string templatesDir` (absolute path to the bundled `wwwroot/okf`), both guarded with `ArgumentException.ThrowIfNullOrWhiteSpace`. `CreateLibraryAsync`:
  1. Compute `folderName = SanitizeFolderName(project.Name)`; `dir = Path.Combine(rootPath, folderName)`; `Directory.CreateDirectory(dir)`.
  2. Copy `theme.css`: read `Path.Combine(templatesDir, "theme.css")` bytes and atomic-write to `dir/theme.css` (preserve bytes exactly — use `File.Copy` with overwrite via a temp file + move, or read/write bytes; do **not** re-encode).
  3. Read `Path.Combine(templatesDir, "index.html")` template; fill `{{LIBRARY_TITLE}}` (all 3) with `project.Name`, `{{THEME_HREF}}` with `theme.css` (relative, root-level), `{{LIBRARY_DESCRIPTION}}` with `project.Description ?? ""`; **remove** the sample `<section class="okf-index-group">…</section>` block (no concepts yet). Atomic-write to `dir/index.html` as UTF-8 no BOM.
  Reuse `AtomicWriteAsync` (temp `.tmp` + `File.Move(overwrite:true)`), `Utf8NoBom`, and a `SanitizeFolderName` that rejects `/`, `\`, `..`, empty, and `.`/`..` names (folder names allow spaces, unlike the file-name regex).
- **PATTERN**: `SkipWatch.Core\Services\Wiki\WikiFileStore.cs` (lines 15–16 encoding/regex, 144–157 traversal guard + atomic write). Adapt `SanitizeFileName` (lines 159–172) into a **folder** sanitizer that permits spaces (do not apply `^[A-Za-z0-9._-]+$` to folder names — that would reject `Wild Flowers`).
- **IMPORTS**: `System.IO`, `System.Text`, `System.Text.RegularExpressions` (only if you keep a regex for the traversal check), `VideoCortex.Core.Entities`.
- **GOTCHA**: **Relative theme link only** — `href="theme.css"`, never `/theme.css` (a leading `/` breaks `file://`, SPEC §3.2). The resulting `index.html` MUST parse as OKF-conformant: `okf-meta` present, `type` non-empty (`"Index"`), `okf_html_version: "0.1"`, relative links. When removing the sample group block, ensure the `<main>` still closes cleanly (leave `<h1>` + `<p class="description">`). Never enumerate `rootPath` for siblings; only ever touch `dir`.
- **VALIDATE**: create `VideoCortex.Tests/Library/OkfLibraryStoreTests.cs` — construct the store against a temp `rootPath` and the real `wwwroot/okf` templates dir (locate it relative to the test assembly or copy the three files into a temp templatesDir in the test), call `CreateLibraryAsync(new Project{ Name="Test Lib", Description="d" })`, assert: `theme.css` exists and byte-equals the source; `index.html` exists, contains `id="okf-meta"`, parses the meta JSON with `type=="Index"` and `okf_html_version=="0.1"`, contains `href="theme.css"` (no `/theme.css`), title/h1 == `Test Lib`, and contains **no** `okf-index-group`. Then `cd /c/Repos/VideoCortex && dotnet test VideoCortex.slnx --filter "FullyQualifiedName~OkfLibraryStore"` exits 0.

#### Task 4: Sibling-folder safety test

Prove the store never touches a pre-existing sibling library.

- **IMPLEMENT**: add a test in `OkfLibraryStoreTests.cs`: seed a temp `rootPath` with a fake `Wild Flowers/` folder containing a sentinel `index.html` and `theme.css` with known contents; call `CreateLibraryAsync` for a **different** project (`Name="Other"`); assert the `Wild Flowers` files are byte-for-byte unchanged and that `CreateLibraryAsync` only created `rootPath/Other/`. Also assert `CreateLibraryAsync` on a project whose name would collide with an existing sibling folder writes **into that folder** (overwriting only `index.html`/`theme.css`) — document that folder-name collisions are the caller's (ProjectService's) responsibility to prevent via unique Name, and the store itself is idempotent-by-name.
- **PATTERN**: n/a (new safety invariant; the SPEC §9 permissive/precious-artifact ethos and PRD §8 filesystem-safety row).
- **IMPORTS**: `System.IO`, `FluentAssertions`, `Xunit`.
- **GOTCHA**: This is the single most important safety property in the product (PRD Risk: "App writes into or corrupts an existing hand-built library"). The test must fail loudly if the store ever recursively deletes or rewrites an unrelated sibling.
- **VALIDATE**: `cd /c/Repos/VideoCortex && dotnet test VideoCortex.slnx --filter "FullyQualifiedName~OkfLibraryStore"` exits 0.

### Milestone 2: Project CRUD service

A `ProjectService` creates (with library scaffolding), lists, fetches-by-slug, and deletes projects; unit-tested on the in-memory SQLite fixture.

**Validation checkpoint**: `dotnet test VideoCortex.slnx --filter "FullyQualifiedName~ProjectService"` passes, including slug-collision and delete cases.

#### Task 5: Result records & form model

Define the DTOs and form model the service and UI exchange.

- **IMPLEMENT**: in `VideoCortex.Features.Projects.Models`:
  - `ProjectFormModel` (class with init/get-set props + data annotations): `[Required] string Name`, `string? Description`, `string? AIInstructions`.
  - `ProjectSummaryDto(int Id, string Name, string Slug, string? Description, int VideoCount, DateTime? ReportUpdatedAt)`.
  - `ProjectDetailDto(int Id, string Name, string Slug, string? Description, string? AIInstructions, ProjectStatus Status, DateTime? ReportUpdatedAt, DateTime CreatedAt, string LibraryFolderName)` — `LibraryFolderName` is the sanitized on-disk folder name so the detail page can render an "Open library ↗" link/path.
  - `SaveProjectResult(bool Success, ProjectSummaryDto? Project, string? ErrorMessage, bool IsDuplicate)`.
  - `DeleteProjectResult(bool Success, string? ErrorMessage)`.
- **PATTERN**: SkipWatch `SaveProjectResult` / `ProjectSummaryDto` / `DeleteProjectResult` records (referenced in `ProjectService.cs`).
- **IMPORTS**: `System`, `System.ComponentModel.DataAnnotations`, `VideoCortex.Core.Entities` (for `ProjectStatus`).
- **GOTCHA**: `ProjectFormModel` must be a class (not positional record) so Blazor `EditForm` two-way binding + `DataAnnotationsValidator` work. DTOs may be positional records.
- **VALIDATE**: `cd /c/Repos/VideoCortex && dotnet build VideoCortex` exits 0.

#### Task 6: `ProjectService`

Implement CRUD over the DbContext, scaffolding the library on create and reverting the folder name on delete (opt-in).

- **IMPLEMENT**: `VideoCortex.Features.Projects.Services.IProjectService` + `ProjectService`. Ctor DI: `VideoCortexDbContext`, `IOkfLibraryStore`, `ILogger<ProjectService>`.
  - `CreateAsync(ProjectFormModel model, CancellationToken)`: trim/normalize Name/Description/AIInstructions; reject empty Name; duplicate check on `Name` (return `IsDuplicate`); compute slug via `SlugHelper.ToSlug` + `UniqueSlugAsync` against `Projects.AnyAsync(p => p.Slug == s)` with fallback `proj-{count+1}`; build `Project { Name, Slug, Description, AIInstructions, Status = ProjectStatus.Idle, CreatedAt = DateTime.UtcNow }`; **save the row first**, catch `DbUpdateException` (race → re-check duplicate); **then** call `_library.CreateLibraryAsync(project, ct)` in a try/catch that logs but does not fail the create (the row is the source of truth; the folder is repairable). Return `SaveProjectResult`.
  - `ListAsync(CancellationToken)` → `AsNoTracking().Select(...)` into `ProjectSummaryDto[]` ordered by `CreatedAt` desc, with `VideoCount = p.Videos.Count`.
  - `GetBySlugAsync(string slug, CancellationToken)` → `AsNoTracking()` project into `ProjectDetailDto?` (compute `LibraryFolderName` from `Name` via the same sanitizer the store uses — expose it as a `static` on `OkfLibraryStore` or a shared helper so the two agree).
  - `DeleteAsync(int projectId, bool deleteLibraryFolder, CancellationToken)`: load project; remove DB row (Videos cascade per Phase 1 config); `SaveChangesAsync`; **only if `deleteLibraryFolder` is true**, best-effort delete `RootPath\<folderName>\` in a try/catch (never fails the op). Default caller passes `deleteLibraryFolder: false`. Return `DeleteProjectResult`.
- **PATTERN**: `SkipWatch.Core...\ProjectService.cs` — `CreateAsync` (41–126), `DeleteAsync` (309–392, DB-first then best-effort file cleanup), `GetForEditAsync` projection (394–400), `Normalize` (402–406). **Drop** all YouTube/channel/transcript/VideoProjects logic.
- **IMPORTS**: `Microsoft.EntityFrameworkCore`, `Microsoft.Extensions.Logging`, `VideoCortex.Core.Db`, `VideoCortex.Core.Entities`, `VideoCortex.Core.Services.Library`, `VideoCortex.Core.Services.Utilities`, `VideoCortex.Features.Projects.Models`.
- **GOTCHA**: Folder deletion is **opt-in and off by default** (PRD §6.1, §8, Risk row). The default delete path removes DB rows only; the on-disk OKF library is precious and survives. The folder deleted (when opted-in) is only ever `RootPath\<sanitized Name>\` — never enumerate siblings. Guard against the folder name being empty/`.`/`..` before any delete. Slug uniqueness is enforced by the unique index from Phase 1; the service resolves collisions *before* insert but must still catch the `DbUpdateException` race.
- **VALIDATE**: create `VideoCortex.Tests/Projects/ProjectServiceTests.cs` using the Phase-1 in-memory SQLite fixture + a fake/temp-dir `IOkfLibraryStore`: (a) `CreateAsync` returns Success and produces a slug + a scaffolded folder; (b) creating two projects named "Local LLM" yields the second as `IsDuplicate`; (c) creating "A/B" and "A B" yields distinct slugs (`a-b`, `a-b-2`); (d) `DeleteAsync(id, deleteLibraryFolder:false)` removes the row but leaves the folder on disk; (e) `DeleteAsync(id, deleteLibraryFolder:true)` removes the folder. Then `cd /c/Repos/VideoCortex && dotnet test VideoCortex.slnx --filter "FullyQualifiedName~ProjectService"` exits 0.

### Milestone 3: Blazor UI & DI wiring

Projects can be created, listed, opened, and deleted from the browser; the detail page links to the on-disk library and reserves a slot for Phase 3's video table.

**Validation checkpoint**: app boots; `POST`-driven create produces a project + folder; `/projects` lists it; `/projects/{slug}` renders; delete removes the row.

#### Task 7: DI wiring & navigation

Register the services and add a Projects nav entry.

- **IMPLEMENT**: in `Program.cs`, after config binding: register `IOkfLibraryStore` as a **singleton** constructed from `LibrarySettings.RootPath` (resolve `IOptionsMonitor<LibrarySettings>.CurrentValue.RootPath` — or `IOptions<LibrarySettings>`) and the templates dir `Path.Combine(builder.Environment.WebRootPath, "okf")`; register `IProjectService` as **scoped**. Ensure `builder.Environment.WebRootPath` is non-null at registration (it is once `WebApplicationBuilder` is built; if constructing the store needs the built `app`, use a factory `sp => new OkfLibraryStore(...)` reading `IWebHostEnvironment` + `IOptions<LibrarySettings>` from `sp`). Add a "Projects" link to the nav in `MainLayout.razor` (or the nav component) pointing at `/projects`.
- **PATTERN**: SkipWatch `Program.cs` DI registration block; Phase 1 `Program.cs` (already wires DbContext + config).
- **IMPORTS**: `VideoCortex.Core.Services.Library`, `VideoCortex.Core.Services.Utilities`, `VideoCortex.Features.Projects.Services`, `Microsoft.Extensions.Options`.
- **GOTCHA**: `IOkfLibraryStore` is a singleton but must read the **current** `RootPath` if Settings (Phase 7) later changes it — prefer resolving `RootPath` lazily inside the store from an injected `IOptionsMonitor<LibrarySettings>` rather than capturing a string at construction, **or** register scoped. For this phase either is fine; document the choice. Ensure `EnsureDataDir()` / library-root existence: `Directory.CreateDirectory(RootPath)` defensively before first write (do not fail if the default `Documents\SecondBrain` root doesn't exist yet).
- **VALIDATE**: `cd /c/Repos/VideoCortex && dotnet build VideoCortex.slnx` exits 0 (DI graph resolves at build; runtime resolution verified in Task 9 smoke).

#### Task 8: Projects list, detail, and add-form pages

Build the three Blazor components.

- **IMPLEMENT**:
  - `AddProjectForm.razor` — `EditForm Model="@Model"` + `DataAnnotationsValidator`, inputs for Name/Description/AIInstructions, a submit button with busy state, `[Parameter] EventCallback OnProjectAdded`. On valid submit call `IProjectService.CreateAsync`; on success reset the model and invoke the callback (and/or show an inline success message); on `IsDuplicate` show a warning; on failure show an error. No Bootstrap — plain semantic markup + app stylesheet classes.
  - `Projects.razor` — `@page "/projects"`, inject `IProjectService`, load list `OnInitializedAsync`, render `AddProjectForm` with `OnProjectAdded=ReloadAsync`, empty-state text, and a list/grid of projects each linking to `/projects/@p.Slug` showing Name, Description, `@p.VideoCount video(s)`.
  - `ProjectDetail.razor` — `@page "/projects/{Slug}"`, `[Parameter] public string Slug`, inject `IProjectService` + `NavigationManager`, load via `GetBySlugAsync`; not-found branch; render a **header** (Name, Description, created date, status), an **"Open library ↗"** anchor whose `href` is a `file://` path to `RootPath\<LibraryFolderName>\index.html` (build with `new Uri(fullPath).AbsoluteUri`), a **`<section>` placeholder** captioned e.g. "Videos — coming in Phase 3" (this is where Phase 3 mounts the video table; leave a clearly-marked TODO comment), and a **Delete** button opening a confirmation prompt. On confirmed delete call `DeleteAsync(id, deleteLibraryFolder:false)` then `NavigationManager.NavigateTo("/projects")`. Include an optional "also delete the library folder on disk" checkbox in the confirm dialog, **unchecked by default**, wired to the `deleteLibraryFolder` argument.
- **PATTERN**: `SkipWatch...\Projects.razor` (list), `ProjectPage.razor` (detail + delete-confirm modal, 114–147), `AddProjectForm.razor` (form). Strip Bootstrap and the tabs/wiki/guides/coverage/edit/rebuild machinery.
- **IMPORTS** (in `_Imports.razor` or per-file): `VideoCortex.Features.Projects.Services`, `VideoCortex.Features.Projects.Models`, `Microsoft.AspNetCore.Components`, `Microsoft.AspNetCore.Components.Forms`.
- **GOTCHA**: The "Open library ↗" link is `file://` — use `new Uri(Path.Combine(rootPath, folderName, "index.html")).AbsoluteUri` and `target="_blank"`. Browsers may block `file://` navigation from an `http://` page; that's an acceptable known limitation for a local-only app — still render the link and also show the plain path as copyable text. The detail page must degrade gracefully if the folder doesn't exist yet (project row created but scaffold failed) — show the path anyway. Default the delete to **rows-only**; the folder-delete checkbox is opt-in.
- **VALIDATE**: `cd /c/Repos/VideoCortex && dotnet build VideoCortex.slnx` exits 0 (Razor compiles). Runtime behavior verified in Task 9.

#### Task 9: End-to-end smoke test

Prove create→list→detail→delete works against the running app and produces a real OKF library.

- **IMPLEMENT**: no new product code — a single non-interactive smoke. Because the create/delete flow is interactive Blazor (SignalR), drive it through the **service layer via a test host** rather than curling the circuit: add an integration test `VideoCortex.Tests/Projects/ProjectFlowSmokeTests.cs` that constructs a real `OkfLibraryStore` against a temp `rootPath` (with the three real templates copied in) + an in-memory SQLite `ProjectService`, then: create a project → assert `RootPath\<Name>\index.html` and `theme.css` exist and the index is OKF-conformant → list → assert it appears → get-by-slug → assert detail fields + `LibraryFolderName` → delete (rows-only) → assert row gone and **folder still present** → delete another with folder-delete → assert folder gone. This is the durable integration proof; keep it in the suite.
- **PATTERN**: Phase 1 in-memory SQLite fixture; `OkfLibraryStoreTests` conformance assertions reused.
- **IMPORTS**: `Microsoft.Data.Sqlite`, `Microsoft.EntityFrameworkCore`, `FluentAssertions`, `Xunit`.
- **GOTCHA**: Do not attempt to script the SignalR circuit in a shell smoke — it is brittle and interactive. The service-level integration test is the correct non-interactive proof and doubles as regression coverage. (An optional additional boot check: start the app on a unique port and assert `/projects` returns HTTP 200 — pattern below in VALIDATION COMMANDS Level 4.)
- **VALIDATE**: `cd /c/Repos/VideoCortex && dotnet test VideoCortex.slnx --filter "FullyQualifiedName~ProjectFlowSmoke"` exits 0.

### Milestone 4: Commit, push, and open PR (mandatory)

**Validation checkpoint**: Branch pushed to origin; PR open against `main`.

#### Task 10: Commit, push, and open PR

- **IMPLEMENT**:
  - Ensure you are on branch `phase-2-projects` off `main` (`git switch -c phase-2-projects` if not already).
  - Stage and commit all changes: message `Phase 2: Projects — CRUD, OKF library scaffolding, slug generation`.
  - Push: `git push -u origin phase-2-projects`.
  - Open PR: `gh pr create --base main --head phase-2-projects --title "Phase 2: Projects" --body "<body>"` where `<body>` is the ACCEPTANCE CRITERIA as a checked checklist + a `## Notes` section.
- **GOTCHA**: **The `origin` remote may not exist yet and `gh` may be unauthenticated** (same precondition as Phase 1). Precondition: a GitHub remote named `origin` must be added (`git remote add origin <url>`) and `gh auth status` must succeed. If neither is set up, stop after the local commit and report that push/PR require a remote — do not fail silently. Do **not** add Claude as an author or `Co-Authored-By` (per repo policy; all commits authored solely by the user).
- **VALIDATE**: `gh pr view --json number,title,state,headRefName` returns the PR with `"state":"OPEN"` and `"headRefName":"phase-2-projects"` (only runnable once a remote exists).

---

## TESTING STRATEGY

### Unit Tests
- `SlugHelper.ToSlug` — casing, hyphen collapse, trim, empty→fallback; `UniqueSlugAsync` — collision increments and GUID escape hatch.
- `OkfLibraryStore.CreateLibraryAsync` — folder created; `theme.css` byte-equals source; `index.html` OKF-conformant (okf-meta parses, `type=="Index"`, `okf_html_version=="0.1"`, relative `href="theme.css"`, no `okf-index-group`, title/h1 == project name); idempotent re-create.
- `ProjectService` — create success + slug assignment; duplicate-name → `IsDuplicate`; distinct slugs for name-collisions; delete rows-only vs delete-with-folder.

### Integration Tests
- `ProjectFlowSmokeTests` — full create→list→get→delete cycle on in-memory SQLite + real `OkfLibraryStore` against a temp root, asserting the on-disk library is conformant and that rows-only delete preserves the folder.
- Optional app-boot check: app starts and serves `/projects` with HTTP 200.

### Edge Cases
- **Sibling-folder safety**: a pre-existing `Wild Flowers/` in the root is never modified or deleted by any operation (Task 4).
- Project name with slashes/`..` (`"A/B"`, `"../x"`) — slug is safe; folder sanitizer rejects traversal but preserves spaces.
- Empty/whitespace name → create rejected with a clear message.
- Duplicate name → `IsDuplicate`, no second folder.
- Delete default (rows-only) leaves the precious folder intact; folder delete only when explicitly opted-in.
- `RootPath` (default `Documents\SecondBrain`) does not exist yet → `Directory.CreateDirectory` creates it defensively; no crash.

---

## VALIDATION COMMANDS

Execute every command to ensure zero regressions and 100% phase correctness.

### Level 1: Syntax & Style
```bash
cd /c/Repos/VideoCortex && dotnet build VideoCortex.slnx -warnaserror
```
**Expected**: exit 0 (warnings-as-errors enforced via `Directory.Build.props`).

### Level 2: Unit Tests
```bash
cd /c/Repos/VideoCortex && dotnet test VideoCortex.slnx --filter "FullyQualifiedName~SlugHelper|FullyQualifiedName~OkfLibraryStore|FullyQualifiedName~ProjectService"
```
**Expected**: exit 0; all slug/store/service unit tests green.

### Level 3: Integration Tests
```bash
cd /c/Repos/VideoCortex && dotnet test VideoCortex.slnx
```
**Expected**: exit 0; includes `ProjectFlowSmokeTests` and the Phase-1 fixtures.

### Level 4: Manual Validation (app boot + /projects reachable)
```bash
cd /c/Repos/VideoCortex && (dotnet run --project VideoCortex --urls http://localhost:5401 & APP=$!; for i in $(seq 1 40); do curl -fsS http://localhost:5401/projects >/dev/null && break; sleep 1; done; curl -fsS http://localhost:5401/projects >/dev/null; RC=$?; kill $APP 2>/dev/null; exit $RC)
```
**Expected**: exit 0; `/projects` serves HTTP 200. (Creating a project through the SignalR circuit is covered by the Task 9 integration test, not this shell smoke.)

---

## ACCEPTANCE CRITERIA

- [ ] `SlugHelper` produces lowercase-hyphenated, `^[A-Za-z0-9._-]+$`-safe slugs with deterministic collision resolution; unit-tested.
- [ ] `IOkfLibraryStore` / `OkfLibraryStore` exists; `CreateLibraryAsync` creates `RootPath\<Project Name>\`, copies `theme.css` byte-for-byte, and writes an OKF-conformant empty root `index.html` (okf-meta `type: "Index"`, `okf_html_version: "0.1"`, title, description, relative `theme.css` link, no index groups). Future `WriteConceptPage`/`WriteReport`/`Delete*` extension points documented but not implemented.
- [ ] The store **never** modifies or deletes a pre-existing sibling folder (`Wild Flowers`); proven by a dedicated safety test.
- [ ] `ProjectService` supports create (with library scaffolding, DB-first), list, get-by-slug, and delete; returns result records; duplicate names and slug collisions handled.
- [ ] Blazor UI: `/projects` lists projects and hosts a create form; `/projects/{slug}` shows the project header, an "Open library ↗" link to the on-disk `index.html`, a **placeholder for the Phase-3 video table**, and a delete action.
- [ ] Project deletion removes DB rows by default; on-disk folder deletion is opt-in and off by default; the folder deleted is only ever the project's own `RootPath\<Name>\`.
- [ ] Folder name is the human display Name (spaces preserved, lightly sanitized); slug is used only for URLs/uniqueness.
- [ ] All validation commands pass; tests green; build clean under warnings-as-errors.

---

## COMPLETION CHECKLIST

- [ ] All tasks completed in order
- [ ] Each task validation passed immediately
- [ ] Level 1: `dotnet build -warnaserror` clean
- [ ] Level 2: slug/store/service unit tests green
- [ ] Level 3: full `dotnet test` green (incl. flow smoke)
- [ ] Level 4: app boots, `/projects` returns 200
- [ ] Sibling-folder safety test present and passing
- [ ] All acceptance criteria met
- [ ] Branch `phase-2-projects` pushed and PR opened against `main` (final task; requires remote)

---

## NOTES

- **Base branch is `main`**, not `master`. Branch: `phase-2-projects` off `main`. Never commit directly to `main`; open a PR. Commits are authored **solely by the user** — do **not** add `Co-Authored-By` (repo policy).
- **No SkipWatch dependency**: SkipWatch is a read-only pattern reference. Video Cortex shares no code or project reference with it. In particular, `WikiFileStore` writes Markdown into `<dataDir>/wiki/<slug>/pages/`; `OkfLibraryStore` writes **HTML into the user's `Documents\SecondBrain\<Name>\` library root** — same atomic-write mechanics, different destination and format.
- **Folder name vs slug** (the one subtle design point): the on-disk **folder** is the human `Name`, lightly sanitized (spaces kept — matches the user's `Wild Flowers` / `Sci Fi Writing`). The **slug** is lowercase-hyphenated and used only for the `/projects/{slug}` route and the DB unique index. `OkfLibraryStore` sanitizes for **folder** safety (reject `/`, `\`, `..`; keep spaces); `SlugHelper` sanitizes for **URL/filename** safety (`^[A-Za-z0-9._-]+$`). Expose the folder-name sanitizer as a shared static so `ProjectService.GetBySlugAsync` (which needs `LibraryFolderName` for the open-library link) and `OkfLibraryStore.CreateLibraryAsync` agree byte-for-byte.
- **Create ordering**: save the `Project` row **first**, then scaffold the folder in a try/catch that logs-but-does-not-fail. Rationale: a disk failure then leaves a row without a folder (repairable by re-running create / a future "repair library" action) rather than an orphan folder with no owning row. Document this if you choose the opposite order.
- **Delete is rows-only by default** (PRD §6.1, §8, Risks). The on-disk OKF library is the precious durable artifact and must survive a project deletion unless the user explicitly opts in via the confirm-dialog checkbox. When opted-in, only `RootPath\<Name>\` is removed — never enumerate or touch siblings.
- **OKF template access at runtime**: templates live under `VideoCortex\wwwroot\okf\` (Phase 1 Task 8). `OkfLibraryStore` reads them from `Path.Combine(IWebHostEnvironment.WebRootPath, "okf")`. If Phase 1 instead placed `index.html`/`concept.html` under a `Templates/` content folder (the documented alternative), point `templatesDir` there — check Phase 1's NOTES/actual layout before wiring DI. `theme.css` is copied byte-for-byte (no re-encode) so generated libraries match hand-built ones.
- **Empty index shell**: for a project with no videos, fill the template's title/description/theme-href and **delete the sample `okf-index-group` section** — an empty library still conforms (SPEC §9 allows missing/empty index groups; a root index just needs a valid `okf-meta` and relative links). Phase 6 will fill this file with the synthesized report; Phase 2 only guarantees a conformant, browsable empty shell.
- **Future extension points** (documented, not built here): `IOkfLibraryStore` grows `WriteConceptPageAsync` (Phase 5, `<video-slug>.html`), `WriteReportAsync` (Phase 6, regenerates root `index.html`), `DeleteConceptAsync` (Phase 6, remove-video), and possibly `DeleteLibraryAsync`. Keep `CreateLibraryAsync` the only real method this phase.
- **`RootPath` may not exist on a fresh machine** — `Directory.CreateDirectory(RootPath)` defensively before the first project write; do not assume `Documents\SecondBrain` pre-exists.
- **Remote/PR precondition**: no `origin` remote may exist at planning time; the final task documents that push + PR require adding a remote and authenticating `gh`. Absent that, the phase ends at a local commit.
