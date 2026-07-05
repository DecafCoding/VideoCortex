# PRD Progress Tracker

This file is the source of truth for the autonomous routine. The routine reads it
on every run, finds the next uncompleted task, executes it, and updates this file
in the same commit. Keep it accurate — if it drifts from reality, the agent drifts.

This file is **generated** from the phase docs in `docs/phases/` by `/create-progress`.
The phase docs are the source of truth for slugs, branch names, dependencies, and
tasks. To change any of those, edit the phase doc and re-run `/create-progress`.
The routine itself is allowed to update task statuses (`[ ]` → `[x]`, blocker lines)
in this file directly — those edits are preserved across regeneration.

## Conventions

- **Phase docs location**: `docs/phases/phase-<N>-<slug>.md`
- **Phase branch naming**: `phase-<N>-<slug>`
- **Commit message format**: `phase <N> task <X>: <short description>`
- **PR title format**: `Phase <N>: <phase name>`
- **PR opens when**: the last task in a phase is checked off
- **Task numbering**: tasks are numbered contiguously within a phase (`Task 1`, `Task 2`, ...) and reset at each new phase. Numbers match the `#### Task N` headings in the phase doc.

## Phase statuses

- `not started` — phase doc exists, no tasks checked off yet
- `in progress` — at least one task checked off, others outstanding
- `complete` — every task in the phase is `[x]`
- `not planned` — phase listed in PRD but has no phase doc; the autonomous routine writes a `> Blocker:` and stops on this phase until `/plan-phase <N>` is run

## Task statuses

- `[ ]` — not started, eligible to be picked up
- `[x]` — complete (validation passed, committed)
- `[~]` — skipped by a human; agent moves past it
- `> Blocker:` line under a task — blocked; agent will not retry until removed

## How to resolve a blocker

The agent leaves a `> Blocker:` line under any task it couldn't finish. To unblock:

1. Read the blocker text on the phase branch.
2. Either fix the underlying issue (edit code, add an env var, clarify the phase doc), or change the task description to be more specific.
3. Delete the `> Blocker:` line.
4. Commit and push to the phase branch.
5. The next routine run will pick the task up again.

To skip a task entirely, change `[ ]` to `[~]`.

---

## Phases

### Phase 1: Scaffold & Foundations
- **Branch**: `phase-1-foundation`
- **Phase doc**: `docs/phases/phase-1-foundation.md`
- **Depends on**: .NET 10 SDK; EF Core 10; OKF templates from `Documents\SecondBrain\okf\templates`. No prior phases.
- **Status**: not started
- **Summary**: Establishes the compiling two-project .NET 10 solution (Blazor Server host + UI-free core), the EF Core SQLite data layer with migrate-on-startup, strongly-typed config with a writable overlay, and the bundled OKF-HTML templates.

Tasks:

- [ ] Task 1: Repo scaffolding and solution
- [ ] Task 2: Entities and enums
- [ ] Task 3: DbContext
- [ ] Task 4: Initial migration
- [ ] Task 5: Runtime paths
- [ ] Task 6: Settings records
- [ ] Task 7: Blazor host + Program.cs wiring
- [ ] Task 8: Bundle OKF templates
- [ ] Task 9: Test bootstrap + foundation tests
- [ ] Task 10: Commit, push, and open PR

### Phase 2: Projects
- **Branch**: `phase-2-projects`
- **Phase doc**: `docs/phases/phase-2-projects.md`
- **Depends on**: Phase 1 (`VideoCortexDbContext`, entities + enums, `VideoCortexPaths`, `LibrarySettings`, bundled OKF templates).
- **Status**: not started
- **Summary**: Delivers the Projects feature — CRUD plus creation of each project's standalone on-disk OKF-HTML library folder (theme.css copy + initial index.html shell), with slug generation and strict sibling-folder safety.

Tasks:

- [ ] Task 1: Slug helper
- [ ] Task 2: `IOkfLibraryStore` interface
- [ ] Task 3: `OkfLibraryStore` implementation
- [ ] Task 4: Sibling-folder safety test
- [ ] Task 5: Result records & form model
- [ ] Task 6: `ProjectService`
- [ ] Task 7: DI wiring & navigation
- [ ] Task 8: Projects list, detail, and add-form pages
- [ ] Task 9: End-to-end smoke test
- [ ] Task 10: Commit, push, and open PR

### Phase 3: Manual Add
- **Branch**: `phase-3-manual-add`
- **Phase doc**: `docs/phases/phase-3-manual-add.md`
- **Depends on**: Phase 1 (entities, DbContext, unique `(ProjectId, YoutubeVideoId)` index, `VideoStatus.Added`); Phase 2 (project detail page). No new packages.
- **Status**: not started
- **Summary**: Makes a project ingestable — the user pastes a YouTube URL (any common shape) or a bare 11-char ID and the app persists a `Video` row at `Status = Added`, with a robust tested parser, duplicate/invalid handling, and a video table on the detail page.

Tasks:

- [ ] Task 1: YouTubeInputParser + result type
- [ ] Task 2: Parser test matrix
- [ ] Task 3: AddVideoResult record
- [ ] Task 4: IVideoCommands.AddVideoByUrlAsync
- [ ] Task 5: IVideoQueries.ListForProjectAsync + VideoRowDto
- [ ] Task 6: Register services + AddVideoForm
- [ ] Task 7: ProjectVideos table + mount on detail page
- [ ] Task 8: VideoCommands persistence tests
- [ ] Task 9: Commit, push, and open PR

### Phase 4: Transcript
- **Branch**: `phase-4-transcript`
- **Phase doc**: `docs/phases/phase-4-transcript.md`
- **Depends on**: Phases 1–3 (Video pipeline-state + metadata columns, `ApifySettings`/`TranscriptWorkerSettings`, `IHttpClientFactory`, the add-video path). External: Apify actor (real call is manual-only, never a CI gate).
- **Status**: not started
- **Summary**: Adds the first background pipeline stage — automatic transcript and metadata retrieval from Apify for `Status = Added` videos via an `ITranscriptSource`/`ApifyTranscriptSource` seam, an ingest runner with backoff/park, and a polling worker; no YouTube Data API.

Tasks:

- [ ] Task 1: Transcript seam + result record
- [ ] Task 2: SRT + duration converters
- [ ] Task 3: ApifyTranscriptSource
- [ ] Task 4: ApifyTranscriptSource + SrtConverter tests (offline)
- [ ] Task 5: TranscriptIngestRunner + interface
- [ ] Task 6: TranscriptIngestRunner tests
- [ ] Task 7: TranscriptWorker + DI registration
- [ ] Task 8: Full suite + boot smoke
- [ ] Task 9: Commit, push, and open PR

### Phase 5: Summary → Concept Page
- **Branch**: `phase-5-summary`
- **Phase doc**: `docs/phases/phase-5-summary.md`
- **Depends on**: Phases 1–4 (`LlmSettings`/`SummarySettings` + overlay; `OkfLibraryStore` + bundled concept template + slug helpers; `Video` transcript/summary columns populated to `Transcribed`). New package: Markdig.
- **Status**: not started
- **Summary**: Adds the per-video summarization stage — a worker calls an OpenAI-compatible LLM (`response_format = json_schema`) for a structured summary, renders the Markdown body with Markdig into the OKF concept template, and writes a per-video concept page, advancing the video to `Summarized`.

Tasks:

- [ ] Task 1: OpenAI-compatible client + per-endpoint cache
- [ ] Task 2: `IVideoSummarizer` + `VideoSummary` + json_schema call
- [ ] Task 3: Summarizer unit test (mocked handler, no network)
- [ ] Task 4: `WriteConceptPageAsync` on `OkfLibraryStore`
- [ ] Task 5: Concept-page writer test (temp dir, conformance)
- [ ] Task 6: `SummaryIngestRunner`
- [ ] Task 7: `SummaryWorker` + DI wiring
- [ ] Task 8: `SummaryIngestRunner` tests (in-memory SQLite, fake summarizer)
- [ ] Task 9: Full build + test sweep
- [ ] Task 10: Commit, push, and open PR

### Phase 6: Report → Root Index
- **Branch**: `phase-6-report`
- **Phase doc**: `docs/phases/phase-6-report.md`
- **Depends on**: Phases 1–5 (`OkfLibraryStore` + root-index template; add/remove + detail surfaces; `OpenAiClient`/cache + json_schema; per-video summaries + concept pages on disk).
- **Status**: not started
- **Summary**: Turns a project's `index.html` into a complete synthesized cross-video report (multi-section semantic HTML citing the per-video concept pages, with a Sources section), regenerated debounced on new summaries and on demand; wires rebuild/open/remove UI.

Tasks:

- [ ] Task 1: `IReportSynthesizer` + contract records
- [ ] Task 2: `OpenAiReportSynthesizer` (json_schema)
- [ ] Task 3: `OkfLibraryStore.WriteReportAsync` + `DeleteConceptPageAsync`
- [ ] Task 4: `Project.ReportDirtySince` column + trigger + migration
- [ ] Task 5: `ReportRegenerationRunner`
- [ ] Task 6: `ReportWorker` (debounced BackgroundService)
- [ ] Task 7: Project-detail UI — rebuild, open, remove
- [ ] Task 8: Synthesizer + writer tests
- [ ] Task 9: Debounce + runner tests
- [ ] Task 10: Commit, push, and open PR

### Phase 7: Settings & Resilience Polish
- **Branch**: `phase-7-settings`
- **Phase doc**: `docs/phases/phase-7-settings.md`
- **Depends on**: Phases 1–6 (config records + overlay `reloadOnChange`; `OpenAiClientCache` driven by `IOptionsMonitor<LlmSettings>`; workers + park logic + project detail page). No new external dependencies.
- **Status**: not started
- **Summary**: The final phase — a writable Settings page that persists config to the overlay and takes effect without a restart (hot reload via `IOptionsMonitor` + client-cache rebuild), plus resilience polish surfacing errored/parked videos with a per-video Retry that routes back to the correct stage.

Tasks:

- [ ] Task 1: `IOverlayWriter` + `OverlayWriter` in `.Core`
- [ ] Task 2: Overlay writer tests
- [ ] Task 3: Settings form model + `SettingsService`
- [ ] Task 4: Settings page UI + DI wiring
- [ ] Task 5: Hot-reload verification (no restart)
- [ ] Task 6: `IVideoRetryCommand` + `VideoRetryCommand` in `.Core`
- [ ] Task 7: Retry command tests (in-memory SQLite)
- [ ] Task 8: Surface parked/errored videos + Retry button on project detail
- [ ] Task 9: Full build + test + boot smoke
- [ ] Task 10: Commit, push, and open PR
