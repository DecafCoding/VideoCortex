# Phase 7: Settings & Resilience Polish

The following plan should be complete, but it is important that you validate documentation and codebase patterns and task sanity before you start implementing.

Pay special attention to naming of existing utils, types, and models. Import from the right files.

## Phase Description

This is the **final phase** of **Video Cortex**. It adds operator-grade configuration and error surfacing on top of the working pipeline built in Phases 1–6:

1. **A writable Settings page** (`VideoCortex/Features/Settings/`) that edits the runtime configuration — Apify token; LLM `Model` / `BaseUrl` / `ApiKey`; `Library:RootPath`; and worker knobs (poll intervals, report debounce, retry cap, request timeouts) — and takes effect **without a process restart**. Non-secret values and secrets alike are persisted to the writable overlay `%USERPROFILE%\.videocortex\appsettings.Local.json` established in Phase 1 (`AddJsonFile(reloadOnChange:true)` + `IOptionsMonitor`), and the per-endpoint `HttpClient` cache from Phase 5 (`OpenAiClientCache`) picks up endpoint/key/model changes on the next call because it is keyed on those values.

2. **Resilience polish**: the project detail page surfaces videos in a failed/parked state (`Status = Error`, `Status = NoTranscript`, or `Parked = true`) with their `LastError`, and a per-video **Retry** button that clears the park state (`Parked = false, RetryCount = 0, NextAttemptAt = null, LastError = null`) and resets the video to the correct **pending** status so the right worker repicks it (a video that failed at the transcript stage goes back to `Added`; one that failed at the summary stage goes back to `Transcribed`).

Because this is the last phase, its acceptance criteria also **confirm the end-to-end MVP success criteria** from PRD §10 are met.

This is a greenfield repository with Phases 1–6 already landed. The reference implementation for every pattern is **SkipWatch** at `C:\Repos\SkipWatch\SkipWatch`. Read those files to copy conventions, then write clean, minimal equivalents — do **not** add a project reference to SkipWatch and do **not** copy its channel/topic/triage/quota/Ops-spend/MCP code. Video Cortex has no channels, topics, wiki jobs, or guides; the Settings surface here is a small subset of SkipWatch's four-tab Settings page.

## User Stories

As the single local user
I want a Settings page where I can enter my Apify token and LLM endpoint/key/model and change the library root and worker timing
So that I can configure the app from the UI without editing JSON files or restarting the process.

As the operator
I want LLM endpoint/credentials to be reconfigurable without a restart
So that I can switch between hosted OpenAI and a local Ollama `/v1` endpoint freely (PRD §4 story 10, §10 success criterion 6).

As the user
I want to see when a video fails or is parked, with the actual error, and retry it in one click
So that a bad video or a transient provider outage does not silently stall my library, and I can recover without touching the database (PRD §4 story 8).

## Problem Statement

After Phase 6 the pipeline runs end-to-end, but configuration is edit-the-JSON-and-restart, and there is no in-app way to see or recover a video that has parked after repeated failures. The two remaining PRD gaps are the writable Settings page (§6.6) and error/park surfacing with per-video retry (§4 story 8, §11 Phase 7). Both are required for the "operator-grade" bar and for the MVP success criteria to be demonstrably met.

## Solution Statement

Add an `IOverlayWriter` service in `.Core` that reads / merges / writes the overlay JSON at `%USERPROFILE%\.videocortex\appsettings.Local.json` atomically (temp file + move-overwrite) **without clobbering unrelated keys**, using the .NET configuration colon-path convention (`"Llm:BaseUrl"` → nested object). Build a `SettingsService` (host feature) that loads current effective values from `IConfiguration` and persists edits through `IOverlayWriter`; wire a `Settings.razor` page with grouped forms. Because the overlay is layered with `reloadOnChange:true` and the LLM stage reads `IOptionsMonitor<LlmSettings>` per call through `OpenAiClientCache` (Phase 5), an endpoint/key/model change is live on the next LLM request with no restart. For resilience, add a `RetryVideoAsync` command in `.Core` (mirroring SkipWatch's unpark reset) that routes the video back to the correct pending status by where it failed, and surface parked/errored videos with their `LastError` and a Retry button on the project detail page. Cover the overlay round-trip/merge/atomicity, the retry command's field-clearing and status routing, and hot-reload binding with tests that use **no network**.

## Phase Metadata

**Phase Type**: New Capability (final phase — settings + resilience)
**Estimated Complexity**: Medium
**Primary Systems Affected**: Configuration overlay + hot reload, Settings UI feature, LLM client cache (Phase 5), project-detail UI, video retry command
**Dependencies**: Phases 1–6. Phase 1 (config records `LlmSettings` / `ApifySettings` / `LibrarySettings` / `TranscriptWorkerSettings` / `SummarySettings` / `ReportSettings`, `VideoCortexPaths.OverlayPath`, overlay layered with `reloadOnChange:true`); Phase 5 (`OpenAiClientCache` keyed on base URL / key / model / timeout, driven by `IOptionsMonitor<LlmSettings>`); Phases 4–6 (workers that pick videos by `Status`, park logic, project detail page + video table). No new external dependencies.

---

## CONTEXT REFERENCES

### Relevant Codebase Files IMPORTANT: YOU MUST READ THESE FILES BEFORE IMPLEMENTING!

These are **SkipWatch reference files** (a separate repo) — read them for patterns to mirror, then write minimal Video Cortex equivalents. Do not reference or depend on the SkipWatch projects.

- `C:\Repos\SkipWatch\SkipWatch\SkipWatch.Core\Services\Config\JsonFileUserConfigStore.cs` — Why: **the exact overlay writer pattern** to mirror as `IOverlayWriter` — colon-path → nested `JsonObject` traversal, `SetStringAsync`/`GetStringAsync`, remove-key-when-empty (so the config chain falls through instead of shadowing with `""`), atomic write via `.tmp` + `File.Move(overwrite:true)`, UTF-8 without BOM, per-instance `SemaphoreSlim` gate. Copy this shape closely.
- `C:\Repos\SkipWatch\SkipWatch\SkipWatch.Core\Services\Config\IUserConfigStore.cs` — Why: the interface contract + the "empty value removes the key" doc note and last-write-wins semantics.
- `C:\Repos\SkipWatch\SkipWatch\SkipWatch.Core\Services\Config\JsonOverlayCredentialStore.cs` — Why: the trade-off decision — on the shipped binary, secrets live in the **same** overlay as tunables (`ICredentialStore` delegates to the config store). We fold this into one `IOverlayWriter` for Video Cortex; document the trade-off in NOTES.
- `C:\Repos\SkipWatch\SkipWatch\SkipWatch\Features\Settings\Services\SettingsService.cs` — Why: load-from-`IConfiguration` / validate / persist-via-store pattern; `ReadInt`/`ReadBool` helpers; base-URL validation (`Uri.TryCreate(..., Absolute)` or empty); returning a `SettingsResult(Ok, Error)`.
- `C:\Repos\SkipWatch\SkipWatch\SkipWatch\Features\Settings\Services\ISettingsService.cs` — Why: the `SettingsResult` record + service interface shape; the "load metadata, don't echo raw secrets back to the UI" note; "persist only dirty secret fields" note.
- `C:\Repos\SkipWatch\SkipWatch\SkipWatch\Features\Settings\Models\TunablesForm.cs` — Why: a plain bind-target class (one property per knob, defaults) for an `EditForm`.
- `C:\Repos\SkipWatch\SkipWatch\SkipWatch\Features\Settings\Components\SettingsModels.razor` + `SettingsModels.razor.cs` — Why: `EditForm` + `InputText`, load-on-init / save-on-submit / reload-after-save, `_saving` flag, "API key set" vs "no API key" badge (never render the raw secret).
- `C:\Repos\SkipWatch\SkipWatch\SkipWatch.Core\Services\OpenAi\OpenAiClientCache.cs` — Why: **proof that no-restart config works** — the client is keyed on `{baseUrl}|{apiKey}|{timeoutSeconds}` and rebuilt only when that key changes; each compiler reads its `IOptionsMonitor` value per call. Confirm Video Cortex's Phase-5 equivalent has this shape; the Settings change is live because of it.
- `C:\Repos\SkipWatch\SkipWatch\SkipWatch.Core\Services\Ops\OpsCommands.cs` — Why: the **unpark reset** to mirror for per-video retry — `Parked = false; RetryCount = 0; NextAttemptAt = null; LastError = null;`, save, then publish a `RetryRequested` change event so the UI/worker react. (SkipWatch resets in place without changing `Status`; Video Cortex additionally routes `Status` back to the correct pending stage — see GOTCHA.)
- `C:\Repos\SkipWatch\SkipWatch\SkipWatch.Core\Services\Ops\IOpsCommands.cs` — Why: interface + XML-doc conventions for the retry command.
- `C:\Repos\SkipWatch\SkipWatch\SkipWatch\Features\Ops\Components\OpsPipeline.razor.cs` — Why: the UI retry handler pattern — `_busy` flag, `await RetryAsync(); await RefreshAsync();` in a `try/finally`.
- `C:\Repos\SkipWatch\SkipWatch\SkipWatch\Program.cs` (lines ~121–128) — Why: DI registration for the overlay store as a singleton pointed at `UserConfigPaths.OverlayPath`, and the dev-vs-prod credential-store split (dev = user-secrets, prod = overlay). Mirror the singleton registration for `IOverlayWriter`.

### Video Cortex files this phase builds on (from Phases 1–6)

- `VideoCortex.Core\Services\Config\VideoCortexPaths.cs` — `OverlayPath` (`%USERPROFILE%\.videocortex\appsettings.Local.json`) and `DefaultLibraryRoot`. Reuse; do not redefine paths.
- `VideoCortex.Core\Services\Config\Settings.cs` — the config records (`ApifySettings`, `LlmSettings`, `LibrarySettings`, `TranscriptWorkerSettings`, `SummarySettings`, `ReportSettings`) with their `public const string Section`. Settings forms map to these section keys.
- `VideoCortex\Program.cs` — where the overlay is layered (`AddJsonFile(VideoCortexPaths.OverlayPath, optional:true, reloadOnChange:true)`) and `Configure<T>` is called. Register `IOverlayWriter` and the Settings service here; add nav to the Settings page.
- `VideoCortex.Core\Services\Llm\OpenAiClientCache.cs` (or equivalent from Phase 5) — the per-endpoint client cache driven by `IOptionsMonitor<LlmSettings>`. **Do not modify** unless it is not already keyed on model/base-url/key/timeout; if it isn't, that's a Phase-5 defect to fix here so hot reload works.
- `VideoCortex\Features\Projects\...` (project detail page + video table from Phases 2/3/6) — where the parked/errored surface and per-video Retry button are added.
- `VideoCortex.Core\Entities\Video.cs` + `Enums.cs` — `VideoStatus { Added, Transcribed, Summarized, Published, NoTranscript, Error }`, plus `Parked`, `RetryCount`, `NextAttemptAt`, `LastError`.

### Relevant Documentation YOU SHOULD READ THESE BEFORE IMPLEMENTING!

- [Options pattern + IOptionsMonitor](https://learn.microsoft.com/en-us/dotnet/core/extensions/options) — `IOptionsMonitor<T>.CurrentValue` re-reads on config reload; this is why overlay edits are live. Why: hot-reload correctness.
- [Configuration in ASP.NET Core — reloadOnChange](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/) — file-provider reload semantics + provider ordering (last provider wins, so the overlay must be layered after `appsettings.json`). Why: overlay precedence + reload trigger.
- [Safe storage of app secrets (user-secrets)](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets) — dev secret storage; **note: user-secrets is NOT hot-reloaded**. Why: the dev-vs-runtime secret story and the GOTCHA below.
- [System.Text.Json.Nodes (JsonObject/JsonNode)](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.nodes) — mutable DOM for the merge-write. Why: overlay writer implementation.
- [EditForm / input components](https://learn.microsoft.com/en-us/aspnet/core/blazor/forms/) — `EditForm`, `InputText`, `InputNumber`, `OnValidSubmit`. Why: Settings UI.
- PRD §6.6 (Settings), §8 (Security & Configuration), §10 (Success Criteria), §11 Phase 7. Why: authoritative scope.

### New Files to Create

- `C:\Repos\VideoCortex\VideoCortex.Core\Services\Config\IOverlayWriter.cs` — interface: `Task<string?> GetAsync(string colonPath, ct)`, `Task SetAsync(string colonPath, string? value, ct)` (empty/null removes the key), `Task<JsonObject> LoadAsync(ct)`. Colon-path traversal contract documented.
- `C:\Repos\VideoCortex\VideoCortex.Core\Services\Config\OverlayWriter.cs` — file-backed implementation (mirror `JsonFileUserConfigStore`): merge into existing overlay, atomic temp+move, UTF-8 no BOM, `SemaphoreSlim` gate, points at `VideoCortexPaths.OverlayPath`.
- `C:\Repos\VideoCortex\VideoCortex.Core\Services\Videos\IVideoRetryCommand.cs` + `VideoRetryCommand.cs` — `Task<RetryResult> RetryVideoAsync(int videoId, ct)`: clears park state, routes `Status` to the correct pending stage, saves. (Place under a `Services\Videos\` or the Phase-3/6 video-commands folder — match wherever Phase 3+ put `AddVideoByUrl`/remove-video; document the chosen namespace.)
- `C:\Repos\VideoCortex\VideoCortex\Features\Settings\Services\ISettingsService.cs` + `SettingsService.cs` — load/validate/persist for the three groups (LLM, Apify, Library+worker knobs); `SettingsResult(bool Ok, string? Error)` record.
- `C:\Repos\VideoCortex\VideoCortex\Features\Settings\Models\SettingsForm.cs` — bind-target class(es): LLM (`Model`, `BaseUrl`, `ApiKey` + `ApiKeyIsSet`/`ApiKeyDirty`), Apify (`Token` + set/dirty), Library (`RootPath`), worker knobs (`TranscriptIdlePollSeconds`, `TranscriptMaxRetryAttempts`, `SummaryIdlePollSeconds`, `SummaryMaxRetryAttempts`, `ReportIdlePollSeconds`, `ReportCoalesceDebounceSeconds`, `ReportMaxRetryAttempts`, `LlmRequestTimeoutSeconds`, `ApifyRunTimeoutSeconds`).
- `C:\Repos\VideoCortex\VideoCortex\Features\Settings\Components\Settings.razor` (+ `.razor.cs`) — routable at `/settings`, grouped `EditForm` sections, save-per-section or one save, success/error message, secrets shown as "set / not set" badges only.
- `C:\Repos\VideoCortex\VideoCortex.Tests\Config\OverlayWriterTests.cs` — round-trip, unrelated-key preservation, empty-value removal, atomicity (temp file gone after write), reload-binding-through-`IOptionsMonitor` where feasible.
- `C:\Repos\VideoCortex\VideoCortex.Tests\Videos\VideoRetryCommandTests.cs` — parked-at-transcript → `Added`, parked-at-summary → `Transcribed`, fields cleared; not-found handled.
- `C:\Repos\VideoCortex\VideoCortex.Tests\Settings\SettingsServiceTests.cs` — validation branches (bad base URL, negative/zero knob, blank library root) + persist-writes-expected-keys against a temp overlay via a fake/real `IOverlayWriter`.

### Existing Files to Modify

- `C:\Repos\VideoCortex\VideoCortex\Program.cs` — register `IOverlayWriter` (singleton at `VideoCortexPaths.OverlayPath`), `ISettingsService`, `IVideoRetryCommand`; add a nav entry to `/settings`.
- `C:\Repos\VideoCortex\VideoCortex\Features\Projects\...ProjectDetail...` (page + code-behind from Phase 2/6) — add the parked/errored row rendering (`LastError`, status) and the Retry button + handler.
- `C:\Repos\VideoCortex\VideoCortex\Components\Layout\...Nav...` — add a "Settings" link.

### Patterns to Follow

**Naming**: PascalCase types/methods, `I`-prefixed interfaces, `record` for results/DTOs, one type per file, colon-path keys matching the Phase-1 record `Section` constants (e.g. `LlmSettings.Section + ":BaseUrl"`).

**Overlay writer** (mirror `JsonFileUserConfigStore`): colon-path split → nested `JsonObject`; `Set` with empty value **removes** the leaf; atomic `.tmp` + `File.Move(overwrite:true)`; UTF-8 no BOM; single `SemaphoreSlim(1,1)` guarding the load-modify-write cycle; **never** rewrite the whole overlay from a fixed object (that would clobber keys other features wrote) — always load-merge-write.

**Settings service** (mirror `SettingsService`): load current values from injected `IConfiguration` (falls through appsettings → overlay), validate, then `await _overlay.SetAsync(key, value, ct)` per field; return `SettingsResult`. For secrets, load only "is it set" (`!string.IsNullOrEmpty(...)`) and persist only when the user actually typed a new value (a `*Dirty` flag), so re-saving the form doesn't wipe a key the user left masked.

**Retry command** (mirror `OpsCommands.UnparkAndSaveAsync` reset, extended): clear `Parked`/`RetryCount`/`NextAttemptAt`/`LastError`; set `Status` back to the pending stage for where it failed; `SaveChangesAsync`; publish the change event so the project page refreshes and the worker's next tick repicks it.

**Blazor forms**: `EditForm Model="_form" OnValidSubmit="HandleSaveAsync"`; `InputText`/`InputNumber`; `_saving`/`_busy` flags disabling the submit; reload the form after a successful save; show secret state as a badge, never the value.

**Warnings-as-errors**: everything compiles clean under `TreatWarningsAsErrors=true`; unused usings and nullable warnings fail the build.

---

## IMPLEMENTATION PLAN

**Rendering**: Milestones — the phase has two independent capability tracks (settings/hot-reload and resilience/retry) plus a shared testing + PR close-out, worth surfacing as checkpoints.

**Rationale**: The overlay writer + Settings page is one integration (config write → reload → live LLM call); the retry command + project-detail surfacing is another (park → surface → reset → repick). Each milestone proves its own end-to-end behavior before the final combined test + PR gate. Tasks execute one at a time, top to bottom; task numbering is contiguous across milestones.

### Milestone 1: Overlay writer & Settings page (hot reload)

The overlay writer merges without clobbering; the Settings page edits LLM/Apify/Library/worker config; changes are live without restart.

**Validation checkpoint**: `dotnet test VideoCortex.slnx --filter FullyQualifiedName~Overlay` passes; a manual/scripted check shows an overlay edit to `Llm:BaseUrl` reflected in `IOptionsMonitor<LlmSettings>.CurrentValue` without restarting the host.

#### Task 1: `IOverlayWriter` + `OverlayWriter` in `.Core`

Create the merge/atomic overlay writer.

- **IMPLEMENT**: `IOverlayWriter` (`LoadAsync`, `GetAsync(colonPath)`, `SetAsync(colonPath, value?)` where null/empty removes the leaf). `OverlayWriter` implementation: constructor takes the overlay path (default `VideoCortexPaths.OverlayPath`); load existing overlay into a `JsonObject` (empty object if file missing/empty); colon-path split → traverse/create nested objects; set or remove leaf; serialize `WriteIndented` to a `.tmp` sibling then `File.Move(tmp, path, overwrite:true)`; UTF-8 without BOM; guard the whole load-modify-write under a `SemaphoreSlim(1,1)`.
- **PATTERN**: `SkipWatch.Core\Services\Config\JsonFileUserConfigStore.cs` (near-verbatim), `IUserConfigStore.cs`.
- **IMPORTS**: `System.Text.Json`, `System.Text.Json.Nodes`, `System.Text`, `System.Threading`, `VideoCortex.Core.Services.Config`.
- **GOTCHA**: **Never** serialize a fresh object over the file — always load-merge so keys other features wrote survive. Empty/null value must **remove** the key (fall through to `appsettings.json`), not write `""`. `Directory.CreateDirectory` on the overlay's parent before the temp write (the `.videocortex` dir exists from Phase 1, but be defensive).
- **VALIDATE**: `cd /c/Repos/VideoCortex && dotnet build VideoCortex.Core` exits 0.

#### Task 2: Overlay writer tests

Prove merge/round-trip/atomicity with no network and no shared global file.

- **IMPLEMENT**: `OverlayWriterTests` writing to a **temp file** (`Path.GetTempFileName()` / a temp dir, deleted in `Dispose`): (a) set `Llm:BaseUrl` then `GetAsync` returns it; (b) pre-seed the file with an unrelated key (`Apify:Token`) then set `Llm:Model` and assert the unrelated key is preserved; (c) set then set-empty removes the leaf; (d) after a write, no `.tmp` file remains beside the overlay; (e) writing to a missing file creates it. Optionally (f) point a `ConfigurationBuilder().AddJsonFile(tempPath, reloadOnChange:false)` at the written file and assert it binds to `LlmSettings`.
- **PATTERN**: `SkipWatch.Tests\Services\Config\JsonFileUserConfigStoreTests.cs`.
- **IMPORTS**: `Xunit`, `FluentAssertions`, `System.Text.Json.Nodes`, `Microsoft.Extensions.Configuration`, `VideoCortex.Core.Services.Config`.
- **GOTCHA**: Use a unique temp path per test (no reliance on `%USERPROFILE%\.videocortex\`) so tests are isolated and CI-safe. No network.
- **VALIDATE**: `cd /c/Repos/VideoCortex && dotnet test VideoCortex.slnx --filter FullyQualifiedName~OverlayWriter` exits 0.

#### Task 3: Settings form model + `SettingsService`

Load current effective config, validate, persist edits through the overlay writer.

- **IMPLEMENT**: `SettingsForm` bind class(es) with LLM (`Model`, `BaseUrl`, `ApiKey`, `ApiKeyIsSet`, `ApiKeyDirty`), Apify (`Token`, `TokenIsSet`, `TokenDirty`), Library (`RootPath`), and worker knobs (poll intervals, retry caps, report debounce, LLM + Apify timeouts). `SettingsResult(bool Ok, string? Error)`. `SettingsService` with `LoadAsync` (read from `IConfiguration` using the Phase-1 `Section` constants; secrets → only `*IsSet` flags, never echo the raw value) and `SaveAsync` (validate → persist changed keys via `IOverlayWriter.SetAsync`). Validation: `Model` non-empty; `BaseUrl` empty or absolute (`Uri.TryCreate(..., Absolute)`); `RootPath` non-empty and a rooted path; every knob within a sane range (poll ≥ 1, retry cap ≥ 0, debounce ≥ 0, timeouts ≥ 1). Persist a secret only when its `*Dirty` flag is set (empty+dirty removes it).
- **PATTERN**: `SkipWatch SettingsService.cs` (`ReadInt`, `IsValidBaseUrl`, dirty-secret persistence), `ISettingsService.cs` (`SettingsResult`), `TunablesForm.cs` (bind class).
- **IMPORTS**: `Microsoft.Extensions.Configuration`, `VideoCortex.Core.Services.Config`, `VideoCortex.Features.Settings.Models`.
- **GOTCHA**: Persist to the same colon keys the config records bind (`LlmSettings.Section + ":Model"`, etc.) or `IOptionsMonitor` won't see them. For `Library:RootPath`, **validate the target is safe** — reject a path that doesn't exist and isn't creatable, and do **not** offer to move/clear an existing non-Video-Cortex folder (see NOTES). Changing `RootPath` affects **new** projects only; existing project folders are not relocated (document in NOTES).
- **VALIDATE**: `cd /c/Repos/VideoCortex && dotnet build VideoCortex` exits 0.

#### Task 4: Settings page UI + DI wiring

Wire the routable page and register services.

- **IMPLEMENT**: `Settings.razor` (`@page "/settings"`) + `.razor.cs`: load on init, grouped `EditForm` sections (LLM endpoint, Apify, Library root, Worker knobs), save handler(s) calling `SettingsService.SaveAsync`, `_saving` guard, success/error banner, secret fields rendered as masked inputs with a "set / not set" badge (mark dirty on input). Register `IOverlayWriter` (singleton at `VideoCortexPaths.OverlayPath`), `ISettingsService`, and (from Milestone 2) `IVideoRetryCommand` in `Program.cs`; add a "Settings" nav link.
- **PATTERN**: `SkipWatch SettingsModels.razor` (+ `.razor.cs`) for the load/save/badge shape; `SkipWatch Program.cs` lines ~123–124 for the singleton overlay registration.
- **IMPORTS**: `Microsoft.AspNetCore.Components`, `Microsoft.AspNetCore.Components.Forms`, `VideoCortex.Features.Settings.*`.
- **GOTCHA**: Register `IOverlayWriter` as a **singleton** (the `SemaphoreSlim` must be shared so concurrent writes serialize). Don't render the raw secret back into the input's value; only a placeholder + badge. Keep the in-app control-panel styling minimal (PRD §7) — this is not an OKF page.
- **VALIDATE**: single non-interactive smoke — `cd /c/Repos/VideoCortex && (dotnet run --project VideoCortex --urls http://localhost:5401 & APP=$!; for i in $(seq 1 40); do curl -fsS http://localhost:5401/settings >/dev/null && break; sleep 1; done; curl -fsS http://localhost:5401/settings >/dev/null; RC=$?; kill $APP 2>/dev/null; exit $RC)` exits 0.

#### Task 5: Hot-reload verification (no restart)

Prove an LLM endpoint/key/model edit takes effect without restarting the process.

- **IMPLEMENT**: A test (preferred) that constructs a `ConfigurationBuilder().AddJsonFile(tempOverlay, reloadOnChange:true)`, builds `IOptionsMonitor<LlmSettings>` via `ServiceCollection` + `Configure<LlmSettings>(config.GetSection(LlmSettings.Section))`, writes a new `Llm:BaseUrl` through `OverlayWriter`, and asserts `monitor.CurrentValue.BaseUrl` reflects it (poll with a short timeout — file-watch reload is async). Additionally assert the Phase-5 `OpenAiClientCache.Get(...)` returns a **different** `HttpClient` after the base URL changes (its cache key includes base URL/key/model/timeout). If a fully deterministic file-watch test proves flaky in CI, replace with a direct `OpenAiClientCache` unit test (two `Get` calls with different base URLs return different clients) **and** a documented manual check in NOTES (edit overlay → observe next LLM call hits the new endpoint). No real network call.
- **PATTERN**: `SkipWatch OpenAiClientCache.cs` (cache-key rebuild); Options docs (IOptionsMonitor reload).
- **IMPORTS**: `Microsoft.Extensions.Configuration`, `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Options`, `Xunit`, `FluentAssertions`.
- **GOTCHA**: `reloadOnChange` fires on a debounce; poll `CurrentValue` for up to a couple seconds rather than asserting immediately. Never open a socket — assert on the cached client's `BaseAddress`/identity, not on a response.
- **VALIDATE**: `cd /c/Repos/VideoCortex && dotnet test VideoCortex.slnx --filter FullyQualifiedName~HotReload` (or `~OpenAiClientCache`) exits 0.

### Milestone 2: Resilience — surface & retry parked/errored videos

Failed/parked videos are visible with their `LastError`; a per-video Retry resets them to the right pending stage and a worker repicks them.

**Validation checkpoint**: `dotnet test VideoCortex.slnx --filter FullyQualifiedName~Retry` passes; a parked video, after Retry, has cleared park fields and the correct pending `Status`.

#### Task 6: `IVideoRetryCommand` + `VideoRetryCommand` in `.Core`

Clear park state and route the video back to the correct pending stage.

- **IMPLEMENT**: `RetryVideoAsync(int videoId, ct)` → load the video; if not found return `RetryResult.Fail`; else clear `Parked = false; RetryCount = 0; NextAttemptAt = null; LastError = null;` and set `Status` to the correct **pending** stage based on where it failed:
  - failed at transcript (`Status` is `Added`, `NoTranscript`, or `Error` with no `TranscriptText`) → `Added` (transcript worker repicks);
  - failed at summary (`Status` is `Transcribed`, or `Error` with `TranscriptText` present but no `SummaryBodyMd`) → `Transcribed` (summary worker repicks);
  - failed at report/publish (`Status` is `Summarized`, or `Error` with `SummaryBodyMd` present) → `Summarized` (report worker repicks).
  Determine the stage from the columns that were populated, not just the raw `Status`, so an `Error` row routes correctly. `SaveChangesAsync`; publish the Phase-4/6 change event so the project page + worker react. Return `RetryResult.Ok`.
- **PATTERN**: `SkipWatch OpsCommands.UnparkAndSaveAsync` (field reset + event publish); extend with the status-routing logic (SkipWatch doesn't reroute status because its stages are inferred differently).
- **IMPORTS**: `Microsoft.EntityFrameworkCore`, `VideoCortex.Core.Db`, `VideoCortex.Core.Entities`.
- **GOTCHA**: The routing must match how **each Phase-4/5/6 worker queries its queue** (e.g. transcript worker `Where(Status == Added)`). Read those workers and route to the exact status they poll — if a worker also filters `!Parked && (NextAttemptAt == null || NextAttemptAt <= now)`, clearing those fields is what makes it eligible again. `NoTranscript` is a terminal-ish state but a retry should re-attempt transcript (Apify may now have subtitles), so route it to `Added`.
- **VALIDATE**: `cd /c/Repos/VideoCortex && dotnet build VideoCortex.Core` exits 0.

#### Task 7: Retry command tests (in-memory SQLite)

Cover field-clearing and status routing with no network.

- **IMPLEMENT**: Using the Phase-1 in-memory SQLite fixture: (a) a video parked at transcript (`Status = Error`, no `TranscriptText`, `Parked = true`, `RetryCount = 3`, `LastError = "boom"`, `NextAttemptAt` set) → after Retry: `Status == Added`, all park fields cleared; (b) parked at summary (`Status = Error`, `TranscriptText` set, `SummaryBodyMd` null) → `Status == Transcribed`; (c) parked at report (`SummaryBodyMd` set) → `Status == Summarized`; (d) `NoTranscript` → `Added`; (e) unknown id → `RetryResult.Fail`, no throw. Assert a change event was published (fake/spy the bus).
- **PATTERN**: SkipWatch Ops/Triage command tests + Phase-1 `SqliteInMemoryFixture`.
- **IMPORTS**: `Microsoft.Data.Sqlite`, `Microsoft.EntityFrameworkCore`, `FluentAssertions`, `Xunit`.
- **GOTCHA**: `:memory:` DB requires one kept-open connection per fixture (Phase 1 pattern). Seed videos with explicit column values; don't rely on worker side effects. No network.
- **VALIDATE**: `cd /c/Repos/VideoCortex && dotnet test VideoCortex.slnx --filter FullyQualifiedName~VideoRetry` exits 0.

#### Task 8: Surface parked/errored videos + Retry button on project detail

Show the failure state and wire the one-click retry.

- **IMPLEMENT**: On the project detail page's video table (Phase 6), for rows where `Status ∈ { Error, NoTranscript }` or `Parked == true`, render the status distinctly and show `LastError` (truncated, with a title/expand for the full text). Add a **Retry** button per such row calling `IVideoRetryCommand.RetryVideoAsync(video.Id)` then refreshing the table; `_busy` guard; success/error feedback. Ensure the row's status live-updates when a worker later advances it (reuse the Phase-4/6 change-event subscription the page already has).
- **PATTERN**: `SkipWatch OpsPipeline.razor.cs` (`_busy` + `await Retry...; await RefreshAsync();` in `try/finally`); the existing project-detail live-refresh subscription.
- **IMPORTS**: `Microsoft.AspNetCore.Components`, `VideoCortex.Core.Services.Videos`, `VideoCortex.Core.Entities`.
- **GOTCHA**: Only show Retry where retrying makes sense (`Error`/`NoTranscript`/`Parked`), not on `Added`/`Transcribed`/`Summarized`/`Published` in-flight rows. Don't block the UI thread — the command is async and the event bus drives the refresh. Truncate `LastError` in the table but make the full message reachable (tooltip/expander) so the operator can diagnose.
- **VALIDATE**: single non-interactive smoke — `cd /c/Repos/VideoCortex && (dotnet run --project VideoCortex --urls http://localhost:5402 & APP=$!; for i in $(seq 1 40); do curl -fsS http://localhost:5402/ >/dev/null && break; sleep 1; done; curl -fsS http://localhost:5402/ >/dev/null; RC=$?; kill $APP 2>/dev/null; exit $RC)` exits 0. (The retry path itself is covered deterministically by Task 7; the smoke only confirms the page still serves.)

### Milestone 3: Full-suite validation & MVP confirmation

Whole suite green; end-to-end MVP success criteria confirmed.

**Validation checkpoint**: `dotnet test VideoCortex.slnx` passes; build clean under warnings-as-errors; the app boots, serves `/`, `/settings`, and a project detail page.

#### Task 9: Full build + test + boot smoke

Run the complete gate across both milestones.

- **IMPLEMENT**: No new production code. Run the full build and test suite; run the boot smoke serving `/`, `/settings`. Fix any regressions surfaced (e.g. a settings key that doesn't match a record `Section`, an overlay write that leaks a `.tmp`).
- **PATTERN**: Phase 1 Level 1/2/4 validation commands.
- **IMPORTS**: n/a.
- **GOTCHA**: Warnings-as-errors — remove unused usings; resolve nullable warnings on the new form/DTO/service files.
- **VALIDATE**: `cd /c/Repos/VideoCortex && dotnet build VideoCortex.slnx -warnaserror && dotnet test VideoCortex.slnx` exits 0.

### Milestone 4: Commit, push, and open PR (mandatory)

**Validation checkpoint**: Branch pushed to origin; PR open against `main`.

#### Task 10: Commit, push, and open PR

- **IMPLEMENT**:
  - Ensure work is on branch `phase-7-settings` (created off `main`).
  - Stage and commit all changes: message `Phase 7: Settings & resilience polish — writable overlay + hot reload, per-video retry`.
  - Push: `git push -u origin phase-7-settings`.
  - Open PR: `gh pr create --base main --head phase-7-settings --title "Phase 7: Settings & Resilience" --body "<body>"` where `<body>` is the ACCEPTANCE CRITERIA as a checked checklist + a `## Notes` section (call out: secrets-in-overlay trade-off, `RootPath` changes affect new projects only, hot-reload verified via test + manual check).
- **PATTERN**: Phase 1 Task 10.
- **GOTCHA**: Same precondition as Phase 1 — **if no `origin` remote exists or `gh auth status` fails, stop after the local commit and report** that push/PR require a remote + authenticated `gh`; do not fail silently. Do not add Claude as an author or `Co-Authored-By` (repo convention).
- **VALIDATE**: `gh pr view --json number,title,state,headRefName` returns the PR with `"state":"OPEN"` and `"headRefName":"phase-7-settings"` (only runnable once a remote exists).

---

## TESTING STRATEGY

### Unit Tests
- `OverlayWriter`: round-trip get/set; unrelated keys preserved on merge; empty value removes the leaf; no `.tmp` left after write; missing-file creation.
- `SettingsService`: validation branches (blank model, non-absolute base URL, blank/unrooted library root, out-of-range knobs) reject; valid save writes the expected colon keys; dirty-secret persistence (masked field left untouched is not wiped).
- `VideoRetryCommand`: field-clearing; status routing per failed stage; unknown id handled; change event published.

### Integration Tests
- Hot reload: overlay write → `IOptionsMonitor<LlmSettings>.CurrentValue` reflects it (polled); `OpenAiClientCache` returns a new client after base-URL change.
- Retry on in-memory SQLite: parked video → Retry → cleared fields + correct pending status; worker-queue query eligibility restored.

### Edge Cases
- Overlay file missing/empty → treated as `{}`; first write creates it.
- Setting a secret to empty (dirty) removes the key (config falls through, not `""`).
- `NoTranscript` retry re-attempts transcript (routes to `Added`).
- `Library:RootPath` pointed at an existing non-Video-Cortex folder → validated/guarded, never deleting or moving sibling content; existing project folders not relocated.
- Concurrent Settings saves serialize via the writer's `SemaphoreSlim` (no corrupt/torn JSON).

### No Network
All tests run offline: no Apify, no OpenAI, no socket opens. LLM-related assertions check the `OpenAiClientCache` client identity/`BaseAddress`, never a response.

---

## VALIDATION COMMANDS

Execute every command to ensure zero regressions and 100% phase correctness.

### Level 1: Syntax & Style
```bash
cd /c/Repos/VideoCortex && dotnet build VideoCortex.slnx -warnaserror
```
**Expected**: exit 0 (warnings-as-errors enforced via Directory.Build.props).

### Level 2: Unit Tests
```bash
cd /c/Repos/VideoCortex && dotnet test VideoCortex.slnx
```
**Expected**: exit 0; overlay, settings, retry, and hot-reload tests green alongside all prior-phase tests.

### Level 3: Integration Tests
Covered by the same `dotnet test` run (hot-reload binding + retry-on-SQLite).

### Level 4: Manual Validation
```bash
cd /c/Repos/VideoCortex && (dotnet run --project VideoCortex --urls http://localhost:5401 & APP=$!; for i in $(seq 1 40); do curl -fsS http://localhost:5401/settings >/dev/null && break; sleep 1; done; curl -fsS http://localhost:5401/settings >/dev/null && curl -fsS http://localhost:5401/ >/dev/null; RC=$?; kill $APP 2>/dev/null; exit $RC)
```
**Expected**: exit 0; `/` and `/settings` both serve HTTP 200.

**Manual hot-reload check (documented, not automated):** with the app running, edit `Llm:BaseUrl` on the Settings page (or in `%USERPROFILE%\.videocortex\appsettings.Local.json`), then trigger a summary — the next LLM call hits the new endpoint with **no restart** (PRD §10 criterion 6).

---

## ACCEPTANCE CRITERIA

- [ ] `IOverlayWriter`/`OverlayWriter` merges into the overlay without clobbering unrelated keys; writes are atomic (temp+move), UTF-8 no BOM; empty value removes the key.
- [ ] Settings page at `/settings` edits Apify token, LLM `Model`/`BaseUrl`/`ApiKey`, `Library:RootPath`, and worker knobs; secrets are shown as "set / not set" (never echoed) and persisted only when changed.
- [ ] Changing the LLM endpoint/key/model takes effect on the next LLM call with **no process restart** (`IOptionsMonitor` + `OpenAiClientCache` rebuild) — verified by test and a documented manual check.
- [ ] Parked/errored videos (`Status = Error`/`NoTranscript` or `Parked`) are surfaced on the project detail page with their `LastError`.
- [ ] Per-video **Retry** clears `Parked`/`RetryCount`/`NextAttemptAt`/`LastError` and routes the video to the correct pending status (transcript-fail → `Added`, summary-fail → `Transcribed`, report-fail → `Summarized`), so the right worker repicks it.
- [ ] All tests run with **no network**; overlay/settings/retry/hot-reload covered; full suite green.
- [ ] Build clean under warnings-as-errors; app boots and serves `/` and `/settings`.
- [ ] **MVP success criteria (PRD §10) confirmed end-to-end**: create project → paste URL → concept page → multi-section report; parked video surfaces + retries; LLM endpoint swap works without restart.
- [ ] Branch `phase-7-settings` pushed and PR opened against `main` (final task; requires remote).

---

## COMPLETION CHECKLIST

- [ ] All tasks completed in order
- [ ] Each task validation passed immediately
- [ ] Level 1: `dotnet build -warnaserror` clean
- [ ] Level 2/3: `dotnet test` green (overlay, settings, retry, hot-reload)
- [ ] Level 4: boot smoke exits 0; `/` and `/settings` serve 200
- [ ] Hot-reload manual check documented/performed
- [ ] All acceptance criteria met (incl. MVP §10 confirmation)
- [ ] Branch pushed and PR opened (final task; requires remote)

---

## NOTES

- **Base branch is `main`** (repo default + the user's PR-based-on-main workflow). This phase branch: `phase-7-settings` off `main`.
- **No SkipWatch dependency**: SkipWatch is a read-only pattern reference; Video Cortex shares no code or project reference with it. This phase mirrors SkipWatch's overlay writer (`JsonFileUserConfigStore`), Settings-service shape, and unpark reset — but implements a smaller surface (no channels/topics/wiki/guides/Ops-spend).
- **Secrets in the overlay — deliberate trade-off**: for this single-user local app, secrets (Apify token, LLM `ApiKey`) are persisted into the same writable overlay (`%USERPROFILE%\.videocortex\appsettings.Local.json`) as non-secret config, exactly as SkipWatch's `JsonOverlayCredentialStore` does. The overlay lives under the user profile, **outside source control**, and the app has no network trust boundary (PRD §8). In **development** the same secrets can be set via `dotnet user-secrets` (higher-priority provider, not written by the app); at **runtime** the Settings page writes the overlay. **user-secrets is NOT hot-reloaded; the overlay IS** — so runtime edits must go to the overlay, and secrets must never be written to the committed `appsettings.json`. If a stricter split is ever wanted, factor a separate `ICredentialStore` (dev = user-secrets, prod = overlay) as SkipWatch does; the single-writer form here is intentionally simpler.
- **Hot reload mechanics**: the overlay is layered last in `Program.cs` (`AddJsonFile(..., reloadOnChange:true)`), so its keys win and a file change raises the reload token. LLM stages read `IOptionsMonitor<LlmSettings>.CurrentValue` per call and hand base-url/key/model/timeout to `OpenAiClientCache`, which rebuilds its `HttpClient` only when that composite key changes. No restart is required. `reloadOnChange` is debounced, so the verification test polls `CurrentValue` briefly rather than asserting synchronously.
- **`Library:RootPath` change semantics**: a new root applies to **projects created after the change**; existing project folders are **not** moved or copied (the DB tracks each project's on-disk location from creation). Document this in the Settings UI help text. **Filesystem safety** (PRD §8/§13): validate the new root is a rooted path that exists or is creatable, and never delete, move, or overwrite unknown sibling content — the app only ever writes inside `RootPath\<project-slug>\`. Guard against pointing at an existing hand-built library folder (e.g. *Wild Flowers*); at minimum do not touch anything the DB didn't create.
- **Atomic overlay writes**: temp file + `File.Move(overwrite:true)` so a crash mid-write can't corrupt the config the app reads on next boot. A single shared `SemaphoreSlim` (singleton `OverlayWriter`) serializes concurrent saves.
- **Retry status routing** depends on the **exact status each Phase-4/5/6 worker polls** — read those workers before finalizing Task 6's mapping; if a worker queries `Where(Status == X && !Parked && (NextAttemptAt == null || NextAttemptAt <= now))`, the retry both resets the status to `X` and clears the eligibility fields.
- **This is the final phase**: its acceptance criteria include confirming the PRD §10 MVP success criteria end-to-end. If any §10 item is not demonstrably met (e.g. report isn't multi-section, or endpoint swap needs a restart), that is a blocker to close-out, not a follow-up.
- **Remote/PR precondition**: as in Phase 1, if no `origin` remote exists or `gh` is unauthenticated, stop after the local commit and report — do not fail silently. Commits are authored solely by the user (no `Co-Authored-By`).
