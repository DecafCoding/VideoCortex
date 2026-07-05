# Phase 3: Manual Add

The following plan should be complete, but it is important that you validate documentation and codebase patterns and task sanity before you start implementing.

Pay special attention to naming of existing utils, types, and models. Import from the right files.

## Phase Description

This phase makes a **Project** ingestable: the user pastes a YouTube URL — in any of its common shapes — or a bare 11-character video ID into a project's detail page, and the app persists a `Video` row with `Status = Added` under that project. It delivers three things:

1. A pure, unit-tested **`YouTubeInputParser`** in `VideoCortex.Core` that normalizes every common YouTube URL form (`youtube.com/watch?v=`, `youtu.be/`, `shorts/`, `embed/`, `live/`, URLs carrying extra query params, and a bare 11-char ID) down to a canonical video ID, returning a result type that distinguishes valid from invalid input.
2. A command method (`IVideoCommands.AddVideoByUrlAsync(projectId, input)`) returning an **`AddVideoResult`** record with three branches — `Added`, `DuplicateInProject`, `InvalidUrl` — that creates a `Video(Status = Added, AddedAt = now)` on the success path, relying on the unique `(ProjectId, YoutubeVideoId)` index from Phase 1 for duplicate detection.
3. A **video table UI** on the Phase 2 project detail page: a paste box plus a table listing each video with its id/title, a status badge, and its added time, with a manual refresh to keep status current.

This phase does **not** fetch transcripts or metadata — that is Phase 4 (Apify). Title, channel, duration, and thumbnail are `null` until Phase 4 backfills them, so the video table must render gracefully with only the video ID and an "Added" state until then.

This is a greenfield repository built on Phase 1 (entities/DbContext) and Phase 2 (project detail page). The reference implementation for every pattern is **SkipWatch** at `C:\Repos\SkipWatch\SkipWatch`. Read those files to copy conventions, then write clean, minimal equivalents — do **not** add a project reference to SkipWatch and do **not** copy its channel/quota/foreground-transcript code (SkipWatch's manual-add path fetches from the YouTube Data API and Apify inline; Video Cortex's does neither in this phase).

## User Stories

As the single local user
I want to paste a YouTube URL or video ID into a project and have a row appear
So that I can queue videos for processing without hunting for IDs or metadata.

As the single local user
I want obviously-bad input rejected with a clear message and duplicates flagged as a no-op
So that I don't create junk or duplicate rows and I know why an add did nothing.

As the single local user
I want to see each video's current pipeline status in a table
So that I can watch a video move from "Added" toward "Published" as later phases run.

## Problem Statement

After Phase 2 a project exists on disk and in the DB, but there is no way to put a video into it. There is no URL parser, no add command, and no video table on the detail page — the project is an empty shell.

## Solution Statement

Add a pure `YouTubeInputParser` (regex-based, mirroring SkipWatch's `YouTubeVideoInputParser.Normalize`) with a rich test matrix, an `IVideoCommands.AddVideoByUrlAsync` command in `.Core` that parses the input, checks for an in-project duplicate against the unique index, and inserts a `Video(Status = Added, AddedAt = now)`, and a `Video` result record (`AddVideoResult`) with `Added` / `DuplicateInProject` / `InvalidUrl` branches. Surface it on the project detail page with a paste box and a status table that renders id-only rows gracefully (no title yet) and refreshes on demand.

## Phase Metadata

**Phase Type**: New Capability
**Estimated Complexity**: Medium
**Primary Systems Affected**: `VideoCortex.Core` domain services (new `Services/Triage/`), the Phase 2 project detail page, the test suite
**Dependencies**: Phase 1 (`Project`/`Video` entities, `VideoCortexDbContext`, unique `(ProjectId, YoutubeVideoId)` index, `VideoStatus.Added`); Phase 2 (project detail page at `/projects/{slug}`). No new NuGet packages.

---

## CONTEXT REFERENCES

### Relevant Codebase Files IMPORTANT: YOU MUST READ THESE FILES BEFORE IMPLEMENTING!

These are **SkipWatch reference files** (a separate repo) — read them for patterns to mirror, then write minimal Video Cortex equivalents. Do not reference or depend on the SkipWatch projects.

- `C:\Repos\SkipWatch\SkipWatch\SkipWatch\Features\Projects\Utilities\YouTubeVideoInputParser.cs` — Why: the exact regex set and `Normalize(string?) → string?` shape to mirror. Video Cortex renames this `YouTubeInputParser`, moves it to `.Core`, and returns a result type instead of a nullable string (so the caller can distinguish "blank/invalid" from "valid"). Keep the same six regexes (watch?v=, youtu.be/, shorts/, embed/, live/, bare-11-char).
- `C:\Repos\SkipWatch\SkipWatch\SkipWatch\Features\Projects\Services\ProjectService.cs` — Why: `AddVideoByYouTubeAsync` (lines ~182–307) is the manual-add reference: parse → find project → dedupe → insert `Video`. **Strip** everything Video Cortex doesn't do in Phase 3: the YouTube Data API lookup (`_yt.GetVideoFullAsync`), the `Channel` creation, and the foreground `_transcripts.RunAsync`. Keep the parse-first, project-exists, duplicate-check, insert, and `DbUpdateException` race-catch structure.
- `C:\Repos\SkipWatch\SkipWatch\SkipWatch\Features\Projects\Models\AddVideoResult.cs` — Why: result-record shape. Video Cortex simplifies to `Added | DuplicateInProject | InvalidUrl` (no quota/existed/attach branches — there is no cross-project sharing here).
- `C:\Repos\SkipWatch\SkipWatch\SkipWatch\Features\Projects\Components\AddVideoForm.razor` — Why: the paste-box `EditForm` + submit-with-spinner + branch-on-result UI pattern to mirror. Drop the quota branch and the "transcript fetched in the foreground" copy.
- `C:\Repos\SkipWatch\SkipWatch\SkipWatch\Features\Projects\Components\ProjectVideos.razor` — Why: the per-video list/table rendering pattern (loading state, empty state, per-row badges). Video Cortex renders a table with id/title, a status badge, and added-time; it must tolerate `Title == null`.
- `C:\Repos\SkipWatch\SkipWatch\SkipWatch.Core\Entities\Video.cs` — Why: confirms the pipeline-state columns (`Status`, `AddedAt`, `Parked`, `RetryCount`) already modeled in Phase 1's `Video`; Phase 3 only writes `YoutubeVideoId`, `ProjectId`, `Status = Added`, `AddedAt`.

### New Files to Create

- `C:\Repos\VideoCortex\VideoCortex.Core\Services\Triage\YouTubeInputParser.cs` — pure static parser returning a `YouTubeInputResult`.
- `C:\Repos\VideoCortex\VideoCortex.Core\Services\Triage\YouTubeInputResult.cs` — `record` distinguishing valid (with `VideoId`) from invalid.
- `C:\Repos\VideoCortex\VideoCortex.Core\Services\Triage\AddVideoResult.cs` — `AddVideoOutcome` enum + `AddVideoResult` record (`Added | DuplicateInProject | InvalidUrl`).
- `C:\Repos\VideoCortex\VideoCortex.Core\Services\Triage\IVideoCommands.cs` — `Task<AddVideoResult> AddVideoByUrlAsync(int projectId, string input, CancellationToken ct = default)`.
- `C:\Repos\VideoCortex\VideoCortex.Core\Services\Triage\VideoCommands.cs` — implementation over `VideoCortexDbContext`.
- `C:\Repos\VideoCortex\VideoCortex.Core\Services\Triage\Dtos\VideoRowDto.cs` — projection for the table (Id, YoutubeVideoId, Title?, Status, AddedAt).
- `C:\Repos\VideoCortex\VideoCortex.Core\Services\Triage\IVideoQueries.cs` + `VideoQueries.cs` — `Task<IReadOnlyList<VideoRowDto>> ListForProjectAsync(int projectId, CancellationToken ct = default)`.
- `C:\Repos\VideoCortex\VideoCortex\Features\Projects\Components\AddVideoForm.razor` — paste box.
- `C:\Repos\VideoCortex\VideoCortex\Features\Projects\Components\ProjectVideos.razor` — video status table + refresh.
- `C:\Repos\VideoCortex\VideoCortex.Tests\Core\Triage\YouTubeInputParserTests.cs` — the parser matrix (first-class).
- `C:\Repos\VideoCortex\VideoCortex.Tests\Core\Triage\VideoCommandsTests.cs` — add / duplicate / invalid persistence tests.

### Existing Files to Modify

- `C:\Repos\VideoCortex\VideoCortex\Program.cs` — register `IVideoCommands`/`VideoCommands` and `IVideoQueries`/`VideoQueries` as scoped services.
- `C:\Repos\VideoCortex\VideoCortex\Features\Projects\Components\ProjectPage.razor` (or the Phase 2 project detail page — confirm the actual filename) — mount `<AddVideoForm>` and `<ProjectVideos>` on the detail page, wiring `OnVideoAdded` to refresh the table.

> If Phase 2 named the detail page or its components differently, discover the real names with a grep (`grep -rl "projects/" VideoCortex/Features/Projects`) before editing — do **not** assume `ProjectPage.razor` exists.

### Relevant Documentation YOU SHOULD READ THESE BEFORE IMPLEMENTING!

- [.NET regular expressions](https://learn.microsoft.com/en-us/dotnet/standard/base-types/regular-expressions) — anchors, groups, `RegexOptions.IgnoreCase`. Why: the parser.
- [EF Core unique indexes](https://learn.microsoft.com/en-us/ef/core/modeling/indexes) + [handling DbUpdateException](https://learn.microsoft.com/en-us/ef/core/saving/concurrency) — Why: duplicate detection via the `(ProjectId, YoutubeVideoId)` index and the save-race catch.
- [Blazor EditForm](https://learn.microsoft.com/en-us/aspnet/core/blazor/forms/) — Why: the paste-box form and submit handling.
- [xUnit `[Theory]`/`[InlineData]`](https://xunit.net/docs/getting-started/netcore/cmdline) — Why: the parser test matrix.

### Patterns to Follow

**Naming**: PascalCase types/methods, `I`-prefixed interfaces, records for DTOs/results, one type per file, enums in declaration order (persisted as ints — do not reorder). Namespaces `VideoCortex` (host) / `VideoCortex.Core` (domain).

**Result-type over exceptions** (mirror SkipWatch `AddVideoResult`): expected outcomes (invalid input, duplicate) are represented in the return record, not thrown. Only genuinely unexpected DB failures surface as caught `DbUpdateException`.

**Pure parser**: `YouTubeInputParser` is a `static` class with compiled `Regex` statics and no DI/DB dependency, so it is trivially unit-testable in isolation.

**Duplicate detection is index-backed**: check `_db.Videos.AnyAsync(v => v.ProjectId == projectId && v.YoutubeVideoId == id)` first, but also catch the `DbUpdateException` from the unique `(ProjectId, YoutubeVideoId)` index on the `SaveChangesAsync` to close the check-then-insert race — the index is the source of truth (mirror SkipWatch's `AddVideo race` catch).

**Warnings-as-errors**: everything must compile clean under `TreatWarningsAsErrors=true`; unused usings and nullable warnings fail the build.

**"Live" status is manual refresh (MVP)**: real push updates (SignalR/event bus) are a later concern. In this phase, `ProjectVideos` re-queries on add and on a "Refresh" button. State this assumption in NOTES.

---

## IMPLEMENTATION PLAN

**Rendering**: Milestones — the phase has natural sub-layers (parser → command/queries → UI → tests) worth surfacing.

**Rationale**: 7 tasks across distinct layers with cross-layer integration (the page must add and display rows through the new services). Milestone checkpoints prove integration beyond each task's isolated VALIDATE.

Tasks execute one at a time, top to bottom. Task numbering is contiguous across milestones.

### Milestone 1: Parser (pure, first-class)

The `YouTubeInputParser` normalizes every supported input shape and rejects garbage, proven by an exhaustive test matrix.

**Validation checkpoint**: `dotnet test VideoCortex.slnx --filter FullyQualifiedName~YouTubeInputParser` is green with the full matrix.

#### Task 1: YouTubeInputParser + result type

Create the pure parser and its result record.

- **IMPLEMENT**: `YouTubeInputResult` record — `record YouTubeInputResult(bool IsValid, string? VideoId)` with static factory helpers `Valid(string id)` and `Invalid`. `YouTubeInputParser` — a `static` class exposing `static YouTubeInputResult Parse(string? input)`. Trim input; return `Invalid` for null/whitespace. Match a bare 11-char ID (`^[A-Za-z0-9_-]{11}$`) first, then in order the six URL regexes: `[?&]v=([A-Za-z0-9_-]{11})` (watch?v=, tolerates extra query params), `youtu\.be/([A-Za-z0-9_-]{11})`, `youtube\.com/shorts/([A-Za-z0-9_-]{11})`, `youtube\.com/embed/([A-Za-z0-9_-]{11})`, `youtube\.com/live/([A-Za-z0-9_-]{11})`, all `RegexOptions.IgnoreCase`. First match's group 1 is the canonical id; no match → `Invalid`.
- **PATTERN**: `SkipWatch\Features\Projects\Utilities\YouTubeVideoInputParser.cs` (same regex set; wrap the nullable-string return in `YouTubeInputResult`).
- **IMPORTS**: `System.Text.RegularExpressions`.
- **GOTCHA**: Anchor the bare-ID regex with `^...$` so a full URL isn't mistaken for an id. Declare `Regex` fields `static readonly` (compiled once). The `v=` regex must use `[?&]` (not `?`) so `?v=` and `&v=` both match; keep it un-anchored so trailing `&t=30s` etc. still matches. An 11-char token containing `/` is impossible (the char class excludes it), so bare-ID and URL cases can't collide.
- **VALIDATE**: `cd /c/Repos/VideoCortex && dotnet build VideoCortex.Core` exits 0.

#### Task 2: Parser test matrix

Cover every supported shape and a broad set of invalid inputs. This is a first-class task — the parser is the correctness core of the phase.

- **IMPLEMENT**: `YouTubeInputParserTests.cs` with `[Theory]`/`[InlineData]`:
  - **Valid → expected id `dQw4w9WgXcQ`**: `dQw4w9WgXcQ` (bare); `https://www.youtube.com/watch?v=dQw4w9WgXcQ`; `http://youtube.com/watch?v=dQw4w9WgXcQ`; `https://www.youtube.com/watch?v=dQw4w9WgXcQ&t=42s&list=PLxxxx` (extra params); `https://youtu.be/dQw4w9WgXcQ`; `https://youtu.be/dQw4w9WgXcQ?si=abc123` (share suffix); `https://www.youtube.com/shorts/dQw4w9WgXcQ`; `https://www.youtube.com/embed/dQw4w9WgXcQ`; `https://www.youtube.com/live/dQw4w9WgXcQ`; `youtu.be/dQw4w9WgXcQ` (no scheme); `  https://youtu.be/dQw4w9WgXcQ  ` (surrounding whitespace, trimmed); a case-preserving id with `-`/`_` e.g. `aB_9-cD3xyz`.
  - **Invalid → `IsValid == false`, `VideoId == null`**: `null`; `""`; `"   "`; `"not a url"`; `"https://vimeo.com/12345"`; `"https://www.youtube.com/watch?v=short"` (too-short id); `"https://www.youtube.com/watch?v=waytoolongvalue123"` (too-long token — the `{11}` group still extracts the first 11 chars, so **assert the documented behavior**: decide in Task 1 whether over-long is Valid-truncated or Invalid, and test accordingly; simplest is to accept the first 11 valid chars like SkipWatch does and test that `watch?v=dQw4w9WgXcQextra` → `dQw4w9WgXcQ`); `"https://www.youtube.com/"`; `"@handle"`; `"dQw4w9WgXc"` (10 chars, bare, too short).
  - Assert `IsValid` and `VideoId` together for each case.
- **PATTERN**: xUnit `[Theory]` (see Phase 1 test conventions); FluentAssertions `result.VideoId.Should().Be(...)`.
- **IMPORTS**: `Xunit`, `FluentAssertions`, `VideoCortex.Core.Services.Triage`.
- **GOTCHA**: `null` in `[InlineData(null, ...)]` needs the parameter typed `string?`. Pin down the over-long / trailing-junk behavior in Task 1 and make the tests assert exactly that — an ambiguous spec here is a real bug source. Whatever you choose, document it in a comment and in NOTES.
- **VALIDATE**: `cd /c/Repos/VideoCortex && dotnet test VideoCortex.slnx --filter FullyQualifiedName~YouTubeInputParser` exits 0.

### Milestone 2: Add command & queries

`.Core` can add a video by URL (with duplicate/invalid handling) and list a project's videos for the table.

**Validation checkpoint**: `dotnet test VideoCortex.slnx --filter FullyQualifiedName~VideoCommands` is green (add / duplicate / invalid).

#### Task 3: AddVideoResult record

Define the result type for the add command.

- **IMPLEMENT**: `AddVideoOutcome` enum in declaration order `{ Added, DuplicateInProject, InvalidUrl }`. `AddVideoResult` record: `record AddVideoResult(AddVideoOutcome Outcome, int? VideoId, string? YoutubeVideoId, string? Message)` with static factory helpers `Added(int videoId, string youtubeId)`, `Duplicate(string youtubeId)`, `Invalid(string message)`. Include a `bool Success => Outcome == AddVideoOutcome.Added` convenience.
- **PATTERN**: `SkipWatch\Features\Projects\Models\AddVideoResult.cs` (simplified — no `IsQuotaExceeded`/`VideoExisted`).
- **IMPORTS**: none beyond BCL.
- **GOTCHA**: Enum order is a persisted-int contract only if stored — this enum is transient (never persisted), so ordering is cosmetic here, but keep it stable for readability. Do not conflate with `VideoStatus`.
- **VALIDATE**: `cd /c/Repos/VideoCortex && dotnet build VideoCortex.Core` exits 0.

#### Task 4: IVideoCommands.AddVideoByUrlAsync

Create the add command over the DbContext.

- **IMPLEMENT**: `IVideoCommands` with `Task<AddVideoResult> AddVideoByUrlAsync(int projectId, string input, CancellationToken ct = default)`. `VideoCommands` (scoped) taking `VideoCortexDbContext` (+ `ILogger<VideoCommands>`):
  1. `var parsed = YouTubeInputParser.Parse(input);` → if `!parsed.IsValid`, return `AddVideoResult.Invalid("Couldn't recognize that as a YouTube video URL or ID.")`.
  2. Confirm the project exists (`_db.Projects.AnyAsync(p => p.Id == projectId, ct)`); if not, return `Invalid($"Project {projectId} not found.")`.
  3. Duplicate check: `if (await _db.Videos.AnyAsync(v => v.ProjectId == projectId && v.YoutubeVideoId == parsed.VideoId, ct))` → `AddVideoResult.Duplicate(parsed.VideoId!)`.
  4. Insert `new Video { ProjectId = projectId, YoutubeVideoId = parsed.VideoId!, Status = VideoStatus.Added, AddedAt = DateTime.UtcNow }`; `SaveChangesAsync`. On success return `AddVideoResult.Added(video.Id, video.YoutubeVideoId)`.
  5. Wrap the save in `try/catch (DbUpdateException)`: on the unique-index violation, re-query for the existing row and return `Duplicate` (closes the check-then-insert race); log at Warning. Rethrow `OperationCanceledException` when `ct.IsCancellationRequested`.
- **PATTERN**: `SkipWatch\ProjectService.AddVideoByYouTubeAsync` — mirror the parse→exists→dedupe→insert→race-catch skeleton, **omitting** the YouTube Data API lookup, `Channel` creation, and foreground transcript fetch.
- **IMPORTS**: `Microsoft.EntityFrameworkCore`, `Microsoft.Extensions.Logging`, `VideoCortex.Core.Db`, `VideoCortex.Core.Entities`.
- **GOTCHA**: Set only the four fields this phase owns (`ProjectId`, `YoutubeVideoId`, `Status`, `AddedAt`); leave `Title`/`ChannelTitle`/`DurationSeconds`/etc. `null` for Phase 4 to backfill. Do not fetch anything. `DateTime.UtcNow` (UTC everywhere, matching the DB convention). Register the service in Task 6.
- **VALIDATE**: `cd /c/Repos/VideoCortex && dotnet build VideoCortex.Core` exits 0.

#### Task 5: IVideoQueries.ListForProjectAsync + VideoRowDto

Create the read side that feeds the table.

- **IMPLEMENT**: `VideoRowDto(int Id, string YoutubeVideoId, string? Title, VideoStatus Status, DateTime AddedAt)`. `IVideoQueries` with `Task<IReadOnlyList<VideoRowDto>> ListForProjectAsync(int projectId, CancellationToken ct = default)`. `VideoQueries` (scoped) over `VideoCortexDbContext`: `AsNoTracking().Where(v => v.ProjectId == projectId).OrderByDescending(v => v.AddedAt).Select(v => new VideoRowDto(v.Id, v.YoutubeVideoId, v.Title, v.Status, v.AddedAt)).ToListAsync(ct)`.
- **PATTERN**: SkipWatch `IVideoQueries` projection-to-DTO style (`AsNoTracking` + `Select`), and `ProjectVideos.razor`'s data shape.
- **IMPORTS**: `Microsoft.EntityFrameworkCore`, `VideoCortex.Core.Db`, `VideoCortex.Core.Entities`.
- **GOTCHA**: Project to the DTO **in the query** (`Select` before `ToListAsync`) so EF translates it and entities aren't tracked/over-fetched. `Title` is nullable by design (Phase 4 backfills). Newest-first ordering matches the "just added shows at top" UX.
- **VALIDATE**: `cd /c/Repos/VideoCortex && dotnet build VideoCortex.Core` exits 0.

### Milestone 3: UI on the project detail page

Paste box + live-status table wired into the Phase 2 detail page.

**Validation checkpoint**: the app builds and serves; the detail page shows the add box and (after an add) a row with an "Added" badge.

#### Task 6: Register services + AddVideoForm

Wire DI and add the paste box.

- **IMPLEMENT**: In `Program.cs`, `builder.Services.AddScoped<IVideoCommands, VideoCommands>();` and `AddScoped<IVideoQueries, VideoQueries>();`. Create `AddVideoForm.razor` (in `VideoCortex\Features\Projects\Components\`): an `EditForm` over an inline `AddInputModel { string? Input }`, an `<InputText>` placeholder `"YouTube URL or video ID"`, a submit button with a busy spinner, and a `[Parameter] int ProjectId` + `[Parameter] EventCallback OnVideoAdded`. On submit, call `IVideoCommands.AddVideoByUrlAsync`, then branch on `result.Outcome`: `Added` → clear the box + `await OnVideoAdded.InvokeAsync()` + show a success message; `DuplicateInProject` → warning message; `InvalidUrl` → error message. Use whatever message/toast mechanism Phase 2 established (or inline `<div class="alert">` if none exists).
- **PATTERN**: `SkipWatch\Features\Projects\Components\AddVideoForm.razor` (drop the quota branch and the foreground-transcript copy; the help text should say the video will be picked up for processing, not that a transcript is fetched now).
- **IMPORTS**: `@inject IVideoCommands`; `VideoCortex.Core.Services.Triage`.
- **GOTCHA**: Guard against double-submit with an `IsBusy` flag disabling the input+button. Do not assume SkipWatch's `IMessageCenterService` exists here — use the Phase 2 pattern or a local alert. Confirm the Components folder path matches Phase 2's convention before creating the file.
- **VALIDATE**: `cd /c/Repos/VideoCortex && dotnet build VideoCortex.slnx` exits 0.

#### Task 7: ProjectVideos table + mount on detail page

Add the status table and wire it into the detail page.

- **IMPLEMENT**: `ProjectVideos.razor`: `[Parameter] int ProjectId`, loads `IVideoQueries.ListForProjectAsync` in `OnParametersSetAsync` (or an explicit `LoadAsync`), renders a loading state, an empty state ("No videos in this project yet."), and a `<table>` with columns **Video** (show `Title` when present, else the `YoutubeVideoId` in a `<code>` element as a placeholder), **Status** (a badge whose text/colour maps from `VideoStatus`), and **Added** (`AddedAt` localized). Expose a public `RefreshAsync()` the parent can invoke, and a "Refresh" button that calls it. On the Phase 2 detail page, mount `<AddVideoForm ProjectId="..." OnVideoAdded="RefreshVideos" />` and `<ProjectVideos @ref="..." ProjectId="..." />`, where `RefreshVideos` calls the table's `RefreshAsync()`.
- **PATTERN**: `SkipWatch\Features\Projects\Components\ProjectVideos.razor` (loading/empty/list structure + per-row badge); adapt list → table, tolerate `Title == null`.
- **IMPORTS**: `@inject IVideoQueries`; `VideoCortex.Core.Services.Triage`, `VideoCortex.Core.Services.Triage.Dtos`, `VideoCortex.Core.Entities`.
- **GOTCHA**: `Title` is `null` until Phase 4 — never `.Trim()`/index it unguarded; fall back to the id. A `VideoStatus → CSS class` map keeps the badge readable (e.g. `Added` = neutral/secondary). "Live" here is manual refresh only — do not build a polling loop or event bus (see NOTES). Ensure the `@ref` component and `RefreshVideos` callback are wired so an add immediately repaints the table.
- **VALIDATE**: single non-interactive snippet — `cd /c/Repos/VideoCortex && (dotnet run --project VideoCortex --urls http://localhost:5399 & APP=$!; for i in $(seq 1 40); do curl -fsS http://localhost:5399/ >/dev/null && break; sleep 1; done; curl -fsS http://localhost:5399/ >/dev/null; RC=$?; kill $APP 2>/dev/null; exit $RC)` exits 0.

### Milestone 4: Testing & Validation

Durable regression suite for the add command's three branches (the parser matrix landed in Task 2).

**Validation checkpoint**: `dotnet test VideoCortex.slnx` passes with the new fixtures.

#### Task 8: VideoCommands persistence tests

Cover add / duplicate / invalid against in-memory SQLite.

- **IMPLEMENT**: `VideoCommandsTests.cs` using the Phase 1 keep-alive in-memory SQLite fixture. Tests: (a) adding a valid URL to a seeded project persists a `Video` with `Status == VideoStatus.Added`, non-default `AddedAt`, the parsed `YoutubeVideoId`, and returns `Outcome == Added`; (b) adding the **same** id twice to one project returns `DuplicateInProject` and leaves exactly one row (assert the unique index holds — the second call must not insert); (c) the **same** id added to **two different** projects yields two rows (no cross-project dedupe); (d) invalid input (`"not a url"`) returns `InvalidUrl` and inserts nothing; (e) an unknown `projectId` returns `InvalidUrl` (or the "project not found" branch) and inserts nothing.
- **PATTERN**: Phase 1 `SqliteInMemoryFixture`; FluentAssertions.
- **IMPORTS**: `Microsoft.EntityFrameworkCore`, `FluentAssertions`, `Xunit`, `VideoCortex.Core.Services.Triage`, `VideoCortex.Core.Entities`.
- **GOTCHA**: Use the real migrated/`EnsureCreated` schema so the unique `(ProjectId, YoutubeVideoId)` index is actually present (that's what makes case (b) meaningful). Seed a `Project` first — the command checks project existence. Keep one open connection per fixture (`:memory:` vanishes otherwise).
- **VALIDATE**: `cd /c/Repos/VideoCortex && dotnet test VideoCortex.slnx --filter FullyQualifiedName~VideoCommands` exits 0.

### Milestone 5: Commit, push, and open PR (mandatory)

**Validation checkpoint**: Branch pushed to origin; PR open against `main`.

#### Task 9: Commit, push, and open PR

- **IMPLEMENT**:
  - Create/switch to branch `phase-3-manual-add` off `main`.
  - Stage and commit all changes: message `Phase 3: Manual add — YouTube input parser, AddVideoByUrl command, video status table`.
  - Push: `git push -u origin phase-3-manual-add`.
  - Open PR: `gh pr create --base main --head phase-3-manual-add --title "Phase 3: Manual Add" --body "<body>"` where `<body>` is the ACCEPTANCE CRITERIA as a checked checklist + a `## Notes` section.
- **GOTCHA**: **If no `origin` remote exists or `gh` is unauthenticated**, this cannot complete. Precondition: a GitHub remote named `origin` must exist (`git remote add origin <url>`) and `gh auth status` must succeed. If neither is set up, stop after the local commit and report that push/PR require a remote — do not fail silently. Never commit directly to `main`; this repo uses a branch + PR workflow.
- **VALIDATE**: `gh pr view --json number,title,state,headRefName` returns the PR with `"state":"OPEN"` and `"headRefName":"phase-3-manual-add"` (only runnable once a remote exists).

---

## TESTING STRATEGY

### Unit Tests
- `YouTubeInputParser`: exhaustive matrix of valid URL shapes (watch?v=, youtu.be/, shorts/, embed/, live/, extra query params, share suffix, no-scheme, whitespace, bare id) and invalid inputs (null/blank/garbage/non-YouTube/wrong-length/handle).
- `AddVideoResult` factory helpers produce the expected `Outcome`.

### Integration Tests
- `VideoCommands` against in-memory SQLite: add persists `Status = Added`; in-project duplicate is a no-op returning `DuplicateInProject`; same id across two projects yields two rows; invalid input and unknown project insert nothing.
- `VideoQueries.ListForProjectAsync` returns rows newest-first with a null `Title` tolerated.

### Edge Cases
- URL with trailing `&t=…&list=…` still parses.
- `youtu.be/<id>?si=…` share suffix parses.
- Over-long / trailing-junk token behavior is pinned and asserted (see Task 2 GOTCHA).
- Duplicate insert race is closed by the unique-index `DbUpdateException` catch.
- Video row renders with only an id (no title) — the table shows the id placeholder.

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
cd /c/Repos/VideoCortex && dotnet test VideoCortex.slnx --filter FullyQualifiedName~YouTubeInputParser
```
**Expected**: exit 0; the full parser matrix passes.

### Level 3: Integration Tests
```bash
cd /c/Repos/VideoCortex && dotnet test VideoCortex.slnx
```
**Expected**: exit 0; `VideoCommands` add/duplicate/invalid tests green alongside the rest of the suite.

### Level 4: Manual Validation
```bash
cd /c/Repos/VideoCortex && (dotnet run --project VideoCortex --urls http://localhost:5399 & APP=$!; for i in $(seq 1 40); do curl -fsS http://localhost:5399/ >/dev/null && break; sleep 1; done; curl -fsS http://localhost:5399/ >/dev/null; RC=$?; kill $APP 2>/dev/null; exit $RC)
```
**Expected**: exit 0; the app serves. Then manually: open a project's detail page, paste `https://youtu.be/dQw4w9WgXcQ`, confirm a row appears with an "Added" badge; paste the same URL again and confirm a duplicate warning and no new row; paste `not a url` and confirm a clear rejection.

---

## ACCEPTANCE CRITERIA

- [ ] `YouTubeInputParser.Parse` normalizes watch?v=, youtu.be/, shorts/, embed/, live/, URLs with extra query params, and a bare 11-char id to a canonical video id; rejects garbage — proven by an exhaustive `[Theory]` matrix.
- [ ] `IVideoCommands.AddVideoByUrlAsync` creates a `Video(Status = Added, AddedAt = now)` under the target project, with `Title`/metadata left null for Phase 4.
- [ ] `AddVideoResult` distinguishes `Added` / `DuplicateInProject` / `InvalidUrl`; the paste box surfaces each branch to the user.
- [ ] In-project duplicates are a no-op (backed by the unique `(ProjectId, YoutubeVideoId)` index, with the save-race caught); the same id in two different projects yields two rows.
- [ ] The project detail page shows a paste box and a video table (id/title, status badge, added time) that tolerates a null title and refreshes on add / on the Refresh button.
- [ ] No transcript or metadata fetch occurs in this phase (deferred to Phase 4).
- [ ] All validation commands pass; tests green under warnings-as-errors.

---

## COMPLETION CHECKLIST

- [ ] All tasks completed in order
- [ ] Each task validation passed immediately
- [ ] Level 1: `dotnet build -warnaserror` clean
- [ ] Level 2: parser matrix green
- [ ] Level 3: full `dotnet test` green
- [ ] Level 4: boot smoke exits 0; manual add/duplicate/invalid behave as specified
- [ ] All acceptance criteria met
- [ ] Branch `phase-3-manual-add` pushed and PR opened against `main` (final task; requires remote)

---

## NOTES

- **Base branch is `main`**, not `master` (repo default + user's PR-based-on-main workflow). This phase branch: `phase-3-manual-add` off `main`.
- **No SkipWatch dependency**: SkipWatch is a read-only pattern reference; Video Cortex shares no code or project reference with it. In particular, SkipWatch's manual-add path fetches from the YouTube Data API and Apify inline — Video Cortex's Phase 3 does **neither**; it only persists an `Added` row for the workers (Phase 4+) to advance.
- **"Live" status = manual refresh (MVP)**: this phase does not implement push updates (SignalR/`JobEventBus`). The table re-queries on add and via a "Refresh" button. Real-time push is deferred; the PRD does not require it for the MVP. State this explicitly so a reviewer doesn't expect auto-updating rows.
- **Metadata is deferred**: `Title`, `ChannelTitle`, `DurationSeconds`, `ThumbnailUrl` stay null until Phase 4 (Apify) backfills them. The table must render id-only rows gracefully — show the `YoutubeVideoId` (in `<code>`) when there's no title.
- **Over-long / trailing-junk parsing decision**: the `{11}` capture groups extract the first 11 valid chars, so `watch?v=dQw4w9WgXcQextrastuff` normalizes to `dQw4w9WgXcQ` (SkipWatch's behavior). Task 1 must decide and document this, and Task 2 must assert whatever is chosen — do not leave it ambiguous.
- **Phase 2 coupling**: the exact detail-page filename and message/toast mechanism come from Phase 2. If Phase 2 named things differently than assumed here (`ProjectPage.razor`, a message service), discover the real names via grep before editing and adapt — do not invent components that Phase 2 didn't create.
- **Duplicate detection is index-backed**: the pre-insert `AnyAsync` check is a courtesy; the unique `(ProjectId, YoutubeVideoId)` index from Phase 1 is the real guard, and the `DbUpdateException` catch closes the check-then-insert race.
- **Remote/PR precondition**: if no `origin` remote exists at execution time, the phase ends at a local commit on `phase-3-manual-add`; document that push + PR require adding a remote and authenticating `gh`.
