# Phase 1: Scaffold & Foundations

The following plan should be complete, but it is important that you validate documentation and codebase patterns and task sanity before you start implementing.

Pay special attention to naming of existing utils, types, and models. Import from the right files.

## Phase Description

This phase establishes the compiling skeleton of **Video Cortex**: a two-project .NET 10 solution (`VideoCortex` Blazor Server host + `VideoCortex.Core` UI-free domain library), the EF Core SQLite data layer (`Project` and `Video` entities + initial migration applied at startup), strongly-typed configuration bound from `appsettings.json` and a writable local overlay, and the bundled OKF-HTML templates copied into `wwwroot/okf/`. No features ship here — the deliverable is a runnable app with a database, config, and template assets in place so every later phase has solid ground.

This is a greenfield repository. There is **no existing Video Cortex code** to mirror; the reference implementation for every pattern is **SkipWatch** at `C:\Repos\SkipWatch\SkipWatch`. Read those files to copy conventions, then write clean, minimal equivalents — do **not** add a project reference to SkipWatch and do **not** copy its channel/topic/triage/quota/MCP code.

## User Stories

As the single local user
I want the app to start up with a working database, configuration, and the OKF templates it needs
So that adding projects and videos in later phases writes to a real, migrated store and produces correctly-styled libraries.

As the operator
I want secrets kept out of source control and config reloadable at runtime
So that I can set my Apify token and LLM key safely and change endpoints without editing code.

## Problem Statement

There is no solution, no data store, no configuration surface, and no output templates. Nothing can be built until these foundations exist and are verified to compile, migrate, and run.

## Solution Statement

Create the two-project solution with `Directory.Build.props` enforcing warnings-as-errors, model the two entities and a `VideoCortexDbContext`, generate and apply an initial EF Core migration on startup, bind config records via `IOptions`/`IOptionsMonitor` from `appsettings.json` plus a writable overlay under `%USERPROFILE%\.videocortex\`, and vendor the OKF `theme.css` + concept/index templates into `wwwroot/okf/`. Verify the whole thing builds, runs, and creates the DB file.

## Phase Metadata

**Phase Type**: New Capability (foundation)
**Estimated Complexity**: Medium
**Primary Systems Affected**: Solution structure, EF Core data layer, configuration, static template assets
**Dependencies**: .NET 10 SDK; EF Core 10 (`Microsoft.EntityFrameworkCore.Sqlite`, `Microsoft.EntityFrameworkCore.Design`); OKF templates from `C:\Users\JasonUser\Documents\SecondBrain\okf\templates`. No prior phases.

---

## CONTEXT REFERENCES

### Relevant Codebase Files IMPORTANT: YOU MUST READ THESE FILES BEFORE IMPLEMENTING!

These are **SkipWatch reference files** (a separate repo) — read them for patterns to mirror, then write minimal Video Cortex equivalents. Do not reference or depend on the SkipWatch projects.

- `C:\Repos\SkipWatch\SkipWatch\SkipWatch.Core\Db\SkipWatchDbContext.cs` — Why: DbContext shape, `OnModelCreating` fluent config, DbSet declarations, index configuration to mirror (drop FTS5 — not needed here).
- `C:\Repos\SkipWatch\SkipWatch\SkipWatch.Core\Entities\Project.cs` — Why: entity conventions (Id, Name, Slug, status enum, nav collection); simplify per our data model.
- `C:\Repos\SkipWatch\SkipWatch\SkipWatch.Core\Entities\Video.cs` — Why: pipeline-state columns (Status, Parked, RetryCount, NextAttemptAt, LastError) we reuse verbatim in spirit.
- `C:\Repos\SkipWatch\SkipWatch\SkipWatch.Core\Services\Config\UserConfigPaths.cs` — Why: the `~/.skipwatch` data-dir + overlay path pattern; mirror as `%USERPROFILE%\.videocortex\`.
- `C:\Repos\SkipWatch\SkipWatch\SkipWatch\Program.cs` — Why: DI composition, `AddRazorComponents().AddInteractiveServerComponents()`, `db.Database.Migrate()` on startup, overlay `AddJsonFile(reloadOnChange:true)`.
- `C:\Repos\SkipWatch\SkipWatch\SkipWatch\appsettings.json` — Why: config section shapes for the settings records.
- `C:\Repos\SkipWatch\SkipWatch\Directory.Build.props` — Why: `TreatWarningsAsErrors`, nullable, target framework settings to replicate at the Video Cortex repo root.

### OKF template source files (copy into the app)

- `C:\Users\JasonUser\Documents\SecondBrain\okf\templates\theme.css` — the shared stylesheet, copied verbatim into `wwwroot/okf/theme.css`.
- `C:\Users\JasonUser\Documents\SecondBrain\okf\templates\concept.html` — the concept page template (placeholders `{{TITLE}}`, `{{THEME_HREF}}`, `{{TYPE}}`, `{{TITLE}}`, `{{DESCRIPTION}}`, `{{TAGS}}`, `{{RESOURCE}}`, `{{TIMESTAMP}}`, `{{TAG_CHIPS}}`, `{{BODY}}`).
- `C:\Users\JasonUser\Documents\SecondBrain\okf\templates\index.html` — the root-index template (`{{LIBRARY_TITLE}}`, `{{THEME_HREF}}`, `{{LIBRARY_DESCRIPTION}}`, group/entry placeholders).
- `C:\Users\JasonUser\Documents\SecondBrain\Wild Flowers\` — a reference library to eyeball for conformance (theme.css + index.html + concept files).

### New Files to Create

- `C:\Repos\VideoCortex\Directory.Build.props` — warnings-as-errors, nullable, `net10.0`.
- `C:\Repos\VideoCortex\VideoCortex.slnx` — solution file referencing the three projects.
- `C:\Repos\VideoCortex\VideoCortex.Core\VideoCortex.Core.csproj` — class library, EF Core packages.
- `C:\Repos\VideoCortex\VideoCortex.Core\Entities\Project.cs`, `Video.cs`, `Enums.cs` (`ProjectStatus`, `VideoStatus`).
- `C:\Repos\VideoCortex\VideoCortex.Core\Db\VideoCortexDbContext.cs`.
- `C:\Repos\VideoCortex\VideoCortex.Core\Db\Migrations\*` — initial migration (generated).
- `C:\Repos\VideoCortex\VideoCortex.Core\Services\Config\VideoCortexPaths.cs` — data-dir + overlay + default library root.
- `C:\Repos\VideoCortex\VideoCortex.Core\Services\Config\Settings.cs` — `ApifySettings`, `LlmSettings`, `LibrarySettings`, `TranscriptWorkerSettings`, `SummarySettings`, `ReportSettings` records.
- `C:\Repos\VideoCortex\VideoCortex\VideoCortex.csproj` — Blazor Server web project referencing `.Core`.
- `C:\Repos\VideoCortex\VideoCortex\Program.cs`, `appsettings.json`, `appsettings.Development.json`.
- `C:\Repos\VideoCortex\VideoCortex\Components\App.razor`, `Routes.razor`, `Components\Layout\MainLayout.razor`, `Components\Pages\Home.razor`, `_Imports.razor`.
- `C:\Repos\VideoCortex\VideoCortex\wwwroot\okf\theme.css`, `concept.html`, `index.html` (copied from OKF templates; `Content`/`CopyToOutputDirectory` as needed).
- `C:\Repos\VideoCortex\VideoCortex.Tests\VideoCortex.Tests.csproj` + `DbContextFactoryTests.cs` (in-memory SQLite fixture).
- `C:\Repos\VideoCortex\.gitignore` (.NET template).

### Relevant Documentation YOU SHOULD READ THESE BEFORE IMPLEMENTING!

- [EF Core SQLite provider](https://learn.microsoft.com/en-us/ef/core/providers/sqlite/) — connection string + provider setup. Why: data layer.
- [Applying migrations at runtime](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying#apply-migrations-at-runtime) — `context.Database.Migrate()` in `Program.cs`. Why: startup migration.
- [Blazor Server project structure (.NET 10)](https://learn.microsoft.com/en-us/aspnet/core/blazor/project-structure) — `App.razor`/`Routes.razor`/interactive server setup. Why: host scaffolding.
- [Options pattern + IOptionsMonitor](https://learn.microsoft.com/en-us/dotnet/core/extensions/options) — bind + hot-reload config. Why: settings records.
- [Safe storage of app secrets (user-secrets)](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets) — dev secret storage. Why: Apify/LLM keys.
- OKF-HTML spec: `C:\Users\JasonUser\Documents\SecondBrain\okf\SPEC.md` — §3 structure, §4 concept docs, §6 index. Why: template fidelity.

### Patterns to Follow

**Naming**: PascalCase types/methods, `I`-prefixed interfaces, records for settings/DTOs, one entity per file, enums in declaration order (matters — persisted as ints).

**Data-dir pattern** (mirror SkipWatch `UserConfigPaths`): a static class resolving `Environment.SpecialFolder.UserProfile` + `.videocortex` for the DB and overlay, and `Environment.SpecialFolder.MyDocuments` + `SecondBrain` as the default library root. Ensure the directory exists before use.

**Startup migration** (mirror SkipWatch `Program.cs`): resolve a scope after `builder.Build()`, get the `DbContext`, call `Database.Migrate()` before `app.Run()`.

**Config overlay**: `builder.Configuration.AddJsonFile(overlayPath, optional:true, reloadOnChange:true)` layered after `appsettings.json`; bind sections with `builder.Services.Configure<T>(...)`; inject `IOptionsMonitor<T>` where hot reload matters (LLM/Apify), `IOptions<T>` elsewhere.

**Warnings-as-errors**: everything must compile clean under `TreatWarningsAsErrors=true`; unused usings and nullable warnings fail the build.

---

## IMPLEMENTATION PLAN

**Rendering**: Milestones — the phase has natural sub-layers (solution scaffold → data → config → host/templates → tests) worth surfacing.

**Rationale**: 9 tasks across four distinct layers with cross-layer integration (the app must build, migrate, and serve). Milestone checkpoints prove integration (e.g., "app boots and creates the DB") beyond each task's isolated VALIDATE.

Tasks execute one at a time, top to bottom. Task numbering is contiguous across milestones.

### Milestone 1: Solution & data layer

Solution compiles; entities + DbContext + initial migration exist.

**Validation checkpoint**: `dotnet build VideoCortex.slnx` succeeds, and `dotnet ef migrations list --project VideoCortex.Core --startup-project VideoCortex` shows the initial migration.

#### Task 1: Repo scaffolding and solution

Create the repo-root build props, `.gitignore`, the three projects, and the solution wiring them together.

- **IMPLEMENT**: `Directory.Build.props` (`<TargetFramework>net10.0</TargetFramework>`, `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`); `VideoCortex.Core` (classlib), `VideoCortex` (`Microsoft.NET.Sdk.Web`, Blazor Server), `VideoCortex.Tests` (xUnit); `VideoCortex.slnx` referencing all three; `VideoCortex` → `.Core` and `VideoCortex.Tests` → `.Core` + `VideoCortex` project references; standard .NET `.gitignore`.
- **PATTERN**: `C:\Repos\SkipWatch\SkipWatch\Directory.Build.props`; SkipWatch `.slnx`.
- **IMPORTS**: n/a (project files).
- **GOTCHA**: Use `.slnx` (XML solution) as SkipWatch does, not `.sln`. Ensure test project references packages `xunit`, `xunit.runner.visualstudio`, `FluentAssertions`, `Microsoft.EntityFrameworkCore.Sqlite`, `Microsoft.NET.Test.Sdk`.
- **VALIDATE**: `cd /c/Repos/VideoCortex && dotnet build VideoCortex.slnx` exits 0.

#### Task 2: Entities and enums

Model `Project` and `Video` per the PRD data model, with the status enums in declaration order.

- **IMPLEMENT**: `Enums.cs` → `ProjectStatus { Idle, Updating, Error }`, `VideoStatus { Added, Transcribed, Summarized, Published, NoTranscript, Error }`. `Project.cs` and `Video.cs` with fields from PRD §9 (Project: Id, Name, Slug, Description?, AIInstructions?, Status, ReportUpdatedAt?, CreatedAt, `ICollection<Video> Videos`; Video: Id, ProjectId, YoutubeVideoId, Title?/ChannelTitle?/ThumbnailUrl?/Description?, DurationSeconds?, ViewCount?/LikeCount?/CommentsCount?, Status, TranscriptText?/TranscriptLang?, SummaryTitle?/SummaryDescription?/SummaryBodyMd?, ConceptSlug?, Parked, RetryCount, NextAttemptAt?, LastError?, AddedAt, TranscribedAt?/SummarizedAt?/PublishedAt?).
- **PATTERN**: `SkipWatch.Core\Entities\Project.cs`, `Video.cs`.
- **IMPORTS**: `System`, `System.Collections.Generic`.
- **GOTCHA**: Enum member order is the persisted int contract — do not reorder later. Initialize `Videos` to `new List<Video>()`.
- **VALIDATE**: `cd /c/Repos/VideoCortex && dotnet build VideoCortex.Core` exits 0.

#### Task 3: DbContext

Create `VideoCortexDbContext` with DbSets and fluent config.

- **IMPLEMENT**: `DbSet<Project>`, `DbSet<Video>`; `OnModelCreating` → unique index on `Project.Slug`, unique index on `Video.(ProjectId, YoutubeVideoId)`, index on `Video.Status` for worker queues, cascade delete Videos with Project, one-to-many Project↔Video. Constructor taking `DbContextOptions<VideoCortexDbContext>`.
- **PATTERN**: `SkipWatch.Core\Db\SkipWatchDbContext.cs` (omit FTS5, triggers, and all non-Project/Video sets).
- **IMPORTS**: `Microsoft.EntityFrameworkCore`, `VideoCortex.Core.Entities`.
- **GOTCHA**: No FTS5 in this project. Keep `OnModelCreating` minimal.
- **VALIDATE**: `cd /c/Repos/VideoCortex && dotnet build VideoCortex.Core` exits 0.

#### Task 4: Initial migration

Generate the initial EF Core migration for the two tables.

- **IMPLEMENT**: Add a design-time `IDesignTimeDbContextFactory<VideoCortexDbContext>` (points at a temp SQLite path so `dotnet ef` can construct the context without the web host), then run `dotnet ef migrations add InitialCreate --project VideoCortex.Core --startup-project VideoCortex --output-dir Db/Migrations`.
- **PATTERN**: SkipWatch migrations under `SkipWatch.Core\Db\` (regular tables only; ignore its FTS5 migration).
- **IMPORTS**: `Microsoft.EntityFrameworkCore.Design`.
- **GOTCHA**: `Microsoft.EntityFrameworkCore.Design` must be a package ref on the startup project (`VideoCortex`) or `.Core`. Ensure `dotnet-ef` tool is available (`dotnet tool install --global dotnet-ef` if the command is missing).
- **VALIDATE**: `cd /c/Repos/VideoCortex && dotnet ef migrations list --project VideoCortex.Core --startup-project VideoCortex` lists `InitialCreate`.

### Milestone 2: Config & paths

Runtime paths + strongly-typed settings exist and bind.

**Validation checkpoint**: a settings smoke test constructs each record and `VideoCortexPaths` returns the expected `.videocortex` DB path and `Documents\SecondBrain` default library root.

#### Task 5: Runtime paths

Create `VideoCortexPaths` resolving data dir, DB path, overlay path, and default library root.

- **IMPLEMENT**: static members: `DataDir` = `%USERPROFILE%\.videocortex`, `DbPath` = `DataDir\app.db`, `OverlayPath` = `DataDir\appsettings.Local.json`, `DefaultLibraryRoot` = `MyDocuments\SecondBrain`. An `EnsureDataDir()` that creates `DataDir` if missing.
- **PATTERN**: `SkipWatch.Core\Services\Config\UserConfigPaths.cs`.
- **IMPORTS**: `System.IO`, `System`.
- **GOTCHA**: Use `Environment.GetFolderPath(SpecialFolder.UserProfile / MyDocuments)`; never hardcode `C:\Users\JasonUser`.
- **VALIDATE**: `cd /c/Repos/VideoCortex && dotnet run --project VideoCortex.Tests --? ` is not applicable — instead: `dotnet build VideoCortex.Core` exits 0 and a temporary inline check: create `VideoCortex.Tests/PathsSmokeTest.cs` asserting `VideoCortexPaths.DbPath.EndsWith(".videocortex\\app.db")` then `dotnet test VideoCortex.slnx --filter FullyQualifiedName~PathsSmoke` exits 0.

#### Task 6: Settings records

Define the config record types.

- **IMPLEMENT**: records `ApifySettings(string Token, int RunTimeoutSeconds=300, string PreferredLanguage="en", bool PreferAutoGenerated=true)`; `LlmSettings(string Model="gpt-4o-mini", string? BaseUrl=null, string? ApiKey=null, int RequestTimeoutSeconds=600)`; `LibrarySettings(string RootPath)` (default from `VideoCortexPaths.DefaultLibraryRoot`); `TranscriptWorkerSettings(int IdlePollSeconds=10, int MaxRetryAttempts=3)`; `SummarySettings(int IdlePollSeconds=10, int MaxRetryAttempts=3)`; `ReportSettings(int IdlePollSeconds=10, int CoalesceDebounceSeconds=10, int MaxRetryAttempts=3)`. Each with a `public const string Section = "...";`.
- **PATTERN**: SkipWatch `SummaryWorkerSettings` / `ApifySettings` records.
- **IMPORTS**: none beyond BCL.
- **GOTCHA**: Records must have parameterless-constructible shape for `Configure<T>` binding — use init-only properties or a class with defaults if positional records don't bind cleanly; prefer `public record ApifySettings { public string Token {get;init;}="" ; ... }` form for config binding.
- **VALIDATE**: `cd /c/Repos/VideoCortex && dotnet build VideoCortex.Core` exits 0.

### Milestone 3: Host, templates, tests

App boots, migrates, serves a page; OKF templates are in `wwwroot`.

**Validation checkpoint**: the app starts, creates `%USERPROFILE%\.videocortex\app.db`, serves HTTP 200 on `/`, and `wwwroot/okf/theme.css` is served 200.

#### Task 7: Blazor host + Program.cs wiring

Scaffold the Blazor Server host, register the DbContext + config, and migrate on startup.

- **IMPLEMENT**: `Program.cs` → `AddRazorComponents().AddInteractiveServerComponents()`; `AddDbContext<VideoCortexDbContext>(o => o.UseSqlite($"Data Source={VideoCortexPaths.DbPath}"))`; layer overlay config file; `Configure<T>` for each settings record; call `VideoCortexPaths.EnsureDataDir()` then `db.Database.Migrate()` in a startup scope; `MapRazorComponents<App>().AddInteractiveServerRenderMode()`. Minimal `App.razor`, `Routes.razor`, `MainLayout.razor`, `Home.razor` (a placeholder "Video Cortex" landing), `_Imports.razor`, `appsettings.json` with the config sections, `appsettings.Development.json`.
- **PATTERN**: `SkipWatch\Program.cs`, `SkipWatch\appsettings.json`, `SkipWatch\Components\*`.
- **IMPORTS**: `Microsoft.EntityFrameworkCore`, `VideoCortex.Core.Db`, `VideoCortex.Core.Services.Config`.
- **GOTCHA**: Enable user-secrets on `VideoCortex.csproj` (`<UserSecretsId>`). Do NOT put the Apify token or LLM key in `appsettings.json` — leave blank. Keep `appsettings.json` sections aligned with the record `Section` names.
- **VALIDATE**: single non-interactive snippet — `cd /c/Repos/VideoCortex && (dotnet run --project VideoCortex --urls http://localhost:5399 & APP=$!; for i in $(seq 1 40); do curl -fsS http://localhost:5399/ >/dev/null && break; sleep 1; done; curl -fsS http://localhost:5399/ >/dev/null; RC=$?; kill $APP 2>/dev/null; test -f "$USERPROFILE/.videocortex/app.db" && exit $RC || exit 1)` exits 0. (On Windows bash, `$USERPROFILE` resolves; if not, substitute `$HOME/.videocortex/app.db`.)

#### Task 8: Bundle OKF templates

Copy the OKF assets into the app and ensure they ship to output.

- **IMPLEMENT**: copy `theme.css`, `concept.html`, `index.html` from `C:\Users\JasonUser\Documents\SecondBrain\okf\templates` into `VideoCortex\wwwroot\okf\`. `theme.css` is served statically; `concept.html`/`index.html` are read at runtime by later phases (mark them `Content` with `CopyToOutputDirectory=PreserveNewest`, or keep under `wwwroot` and read via `IWebHostEnvironment.WebRootPath`). Document the chosen access path in NOTES.
- **PATTERN**: OKF spec §4.2 head boilerplate; existing `Wild Flowers\theme.css`.
- **IMPORTS**: n/a (static assets).
- **GOTCHA**: Preserve template placeholders exactly (`{{THEME_HREF}}` etc.) — later phases string-replace them. Do not minify or reformat `theme.css`; it must byte-match the reference so generated libraries look identical to hand-built ones.
- **VALIDATE**: `cd /c/Repos/VideoCortex && test -f VideoCortex/wwwroot/okf/theme.css && test -f VideoCortex/wwwroot/okf/concept.html && test -f VideoCortex/wwwroot/okf/index.html && grep -q 'okf-meta' VideoCortex/wwwroot/okf/concept.html && echo OK` prints `OK`.

### Milestone 4: Testing & Validation

Durable regression suite: DbContext round-trip + config binding tests.

**Validation checkpoint**: `dotnet test VideoCortex.slnx` passes with the new fixtures.

#### Task 9: Test bootstrap + foundation tests

Establish the in-memory SQLite fixture and cover the data layer + paths.

- **IMPLEMENT**: `SqliteInMemoryFixture` opening a keep-alive `SqliteConnection("DataSource=:memory:")`, creating the schema via `EnsureCreated()`/migrations, yielding a `VideoCortexDbContext`. Tests: (a) insert a `Project` + `Video`, reload, assert relationship + cascade delete; (b) unique-slug constraint throws on duplicate; (c) `VideoCortexPaths` returns expected suffixes; (d) each settings record binds from an in-memory `ConfigurationBuilder`. Remove the temporary `PathsSmokeTest.cs` from Task 5 if superseded.
- **PATTERN**: SkipWatch.Tests in-memory SQLite fixtures.
- **IMPORTS**: `Microsoft.Data.Sqlite`, `Microsoft.EntityFrameworkCore`, `FluentAssertions`, `Xunit`, `Microsoft.Extensions.Configuration`.
- **GOTCHA**: `:memory:` DB vanishes when the connection closes — keep one open connection per fixture for the test's lifetime.
- **VALIDATE**: `cd /c/Repos/VideoCortex && dotnet test VideoCortex.slnx` exits 0.

### Milestone 5: Commit, push, and open PR (mandatory)

**Validation checkpoint**: Branch pushed to origin; PR open against `main`.

#### Task 10: Commit, push, and open PR

- **IMPLEMENT**:
  - Stage and commit all changes: message `Phase 1: Scaffold & foundations — solution, EF Core data layer, config, OKF templates`.
  - Push: `git push -u origin phase-1-foundation`.
  - Open PR: `gh pr create --base main --head phase-1-foundation --title "Phase 1: Scaffold & Foundations" --body "<body>"` where `<body>` is the ACCEPTANCE CRITERIA as a checked checklist + a `## Notes` section.
- **GOTCHA**: **No `origin` remote exists yet and `gh` may be unauthenticated.** Precondition: a GitHub remote named `origin` must be added (`git remote add origin <url>`) and `gh auth status` must succeed. If neither is set up, stop after the local commit and report that push/PR require a remote — do not fail silently.
- **VALIDATE**: `gh pr view --json number,title,state,headRefName` returns the PR with `"state":"OPEN"` and `"headRefName":"phase-1-foundation"` (only runnable once a remote exists).

---

## TESTING STRATEGY

### Unit Tests
- Settings records bind correctly from configuration.
- `VideoCortexPaths` computes expected paths.

### Integration Tests
- DbContext CRUD round-trip on in-memory SQLite; relationship + cascade delete; unique constraints.
- App boots and applies migrations creating `app.db` (Task 7 smoke test).

### Edge Cases
- Duplicate project slug rejected.
- Duplicate `(ProjectId, YoutubeVideoId)` rejected.
- Missing overlay file is tolerated (`optional:true`).

---

## VALIDATION COMMANDS

Execute every command to ensure zero regressions and 100% phase correctness.

### Level 1: Syntax & Style
```bash
cd /c/Repos/VideoCortex && dotnet build VideoCortex.slnx -warnaserror
```
**Expected**: exit 0 (warnings-as-errors already enforced via Directory.Build.props).

### Level 2: Unit Tests
```bash
cd /c/Repos/VideoCortex && dotnet test VideoCortex.slnx
```

### Level 3: Integration Tests
Covered by the same `dotnet test` run (DbContext + boot smoke).

### Level 4: Manual Validation
```bash
cd /c/Repos/VideoCortex && (dotnet run --project VideoCortex --urls http://localhost:5399 & APP=$!; for i in $(seq 1 40); do curl -fsS http://localhost:5399/ >/dev/null && break; sleep 1; done; curl -fsS http://localhost:5399/ >/dev/null; RC=$?; kill $APP 2>/dev/null; exit $RC)
```
**Expected**: exit 0; DB file present at `~/.videocortex/app.db`.

---

## ACCEPTANCE CRITERIA

- [ ] Two-project solution (+ tests) builds clean under warnings-as-errors on .NET 10.
- [ ] `Project` and `Video` entities + `VideoCortexDbContext` exist with correct relationships, indexes, and enums in declaration order.
- [ ] Initial migration generated and applied at startup; running the app creates `~/.videocortex/app.db`.
- [ ] Config records bind from `appsettings.json` + writable overlay; secrets kept out of source (user-secrets enabled, blank in `appsettings.json`).
- [ ] OKF `theme.css` + concept/index templates vendored in `wwwroot/okf/` with placeholders intact and `theme.css` byte-matching the reference.
- [ ] App boots and serves `/` with HTTP 200.
- [ ] All validation commands pass; tests green.

---

## COMPLETION CHECKLIST

- [ ] All tasks completed in order
- [ ] Each task validation passed immediately
- [ ] Level 1: `dotnet build -warnaserror` clean
- [ ] Level 2/3: `dotnet test` green
- [ ] Level 4: boot smoke exits 0, DB created
- [ ] All acceptance criteria met
- [ ] Branch pushed and PR opened (final task; requires remote)

---

## NOTES

- **Base branch is `main`**, not `master` (repo default + user's PR-based-on-main workflow). All phase branches: `phase-<N>-<slug>` off `main`.
- **No SkipWatch dependency**: SkipWatch is a read-only pattern reference; Video Cortex shares no code or project reference with it.
- **`.slnx`** (XML solution) matches the SkipWatch convention and the `dotnet build VideoCortex.slnx` command referenced throughout the phase docs.
- **OKF template access at runtime** (Task 8): default assumption is templates live under `wwwroot/okf/` and are read via `IWebHostEnvironment.WebRootPath`; if the executing agent finds `wwwroot` static-file semantics awkward for server-side reads, moving `concept.html`/`index.html` to a `Templates/` content folder with `CopyToOutputDirectory=PreserveNewest` is an acceptable equivalent — document whichever is chosen.
- **Remote/PR precondition**: no `origin` remote exists at planning time. The final task documents that push + PR require adding a remote and authenticating `gh`; absent that, the phase ends at a local commit.
- **Config binding for records**: positional C# records can be finicky with `IConfiguration.Bind`; prefer init-property record/class shapes with defaults so `Configure<T>` populates them.
