# Video Cortex — Product Requirements Document

**Version 0.1 — Draft**
**Date:** 2026-07-04
**Owner:** Single-user local application

---

## 1. Executive Summary

**Video Cortex** is a single-user, locally installed **.NET 10 Blazor Server** application that turns YouTube videos into a compounding, browsable knowledge library. The user creates a **Project**, pastes in YouTube video URLs, and the app fetches each video's transcript, summarizes it with an LLM, and maintains a per-project **report** that synthesizes everything the project's videos cover.

The library is written to disk as **OKF-HTML** (Open Knowledge Format, HTML profile) — the same format the user already uses for hand-built knowledge libraries in `Documents\SecondBrain` (e.g. *Wild Flowers*, *Sci Fi Writing*). Each project becomes a standalone OKF library folder: one shared-style `theme.css`, a root `index.html` that is a complete synthesized report, and one concept `.html` page per video. The files are plain HTML, opened directly in a browser, and outlive the app itself.

Video Cortex is a deliberately **simplified descendant of SkipWatch**. It keeps SkipWatch's proven pipeline shape (transcript → summary → compiled knowledge, driven by polling background workers) but drops channel following, topic search, discovery cron, triage, quota management, guides, and the MCP server. The MVP goal: **paste a video, get a per-video summary page and a continuously-updated project report, with zero manual HTML authoring.**

### Core value proposition

Turn hours of video into a permanent, auditable, hand-editable knowledge base — one report per research topic — without watching everything and without writing a line of HTML.

---

## 2. Mission

**Mission:** Compound the knowledge locked inside YouTube videos into durable, browsable, per-project reports that grow every time you add a video.

### Core principles

1. **Local-first, single-user.** No accounts, no cloud sync, no notifications. One process on the user's machine. Data lives in a local SQLite DB and plain HTML files on disk.
2. **The artifact outlives the app.** The durable output is OKF-HTML on disk in `Documents\SecondBrain`. If Video Cortex is uninstalled tomorrow, the libraries remain fully usable in any browser.
3. **Uniform, format-conformant output.** Every project is a valid OKF-HTML library and looks the same as the user's existing hand-built ones, because they share the same `theme.css` and templates.
4. **Deterministic where possible, LLM where it adds value.** Templates, metadata, indexes, and file wiring are deterministic. The LLM is used only for the two jobs it is uniquely good at: summarizing a video and synthesizing a cross-video report.
5. **Simplicity over breadth.** This is not SkipWatch. Every feature that isn't required to turn a pasted URL into a knowledge report is out of scope.

---

## 3. MVP Scope

### In Scope

**Core functionality**
- [x] Create / rename / delete a **Project** (each project = one standalone OKF-HTML library)
- [x] Manually add a video to a project by pasting a **YouTube URL or video ID**
- [x] Automatic **transcript retrieval** via Apify
- [x] Automatic **per-video LLM summary**, written as an OKF concept page (`<video-slug>.html`)
- [x] Automatic **per-project synthesized report**, written as the library root `index.html`, regenerated as videos are added
- [x] **"Rebuild report"** action to regenerate a project's `index.html` on demand
- [x] **"Open library ↗"** links from the app to the on-disk `index.html` and individual video pages
- [x] Per-video **pipeline status** surfaced in the UI (Added → Transcribed → Summarized → Published, plus NoTranscript / Error / Parked)
- [x] Remove a video from a project (removes its concept page and regenerates the report)

**Technical**
- [x] Three polling `BackgroundService` workers (transcript, summary, report) with exponential backoff + parking
- [x] SQLite/EF Core state database at `%USERPROFILE%\.videocortex\app.db`
- [x] OKF-HTML output to `Documents\SecondBrain\<Project Name>\`, conformant to OKF-HTML v0.1
- [x] Shipped OKF `theme.css` + concept/index templates, copied into each project on creation

**Configuration**
- [x] Settings page: Apify token, LLM `Model` / `BaseUrl` / `ApiKey`, library root path
- [x] Secrets via user-secrets in development; writable overlay for runtime edits

### Out of Scope (deferred / rejected)

**Rejected (belongs to SkipWatch, not this product)**
- [ ] Channel following / auto-discovery of new videos
- [ ] Topic / saved-search ingestion
- [ ] Discovery cron rounds and YouTube Data API quota management
- [ ] Triage dashboard, Library buckets, Pass action
- [ ] Guides / multi-artifact compilation beyond the single report
- [ ] MCP server
- [ ] Ops page, spend/quota dashboards
- [ ] Sharing a single video across multiple projects (each project is standalone; a re-pasted video is an independent row)

**Deferred (possible v2)**
- [ ] YouTube Data API as a metadata source (MVP uses Apify metadata only)
- [ ] Chunked / map-reduce report synthesis for very large projects (100+ videos)
- [ ] Full-text search across libraries
- [ ] In-app editing of generated pages (user can still hand-edit files on disk)
- [ ] Multi-project cross-linking / a global root index across all projects
- [ ] Non-YouTube sources
- [ ] Packaging as a single-file self-contained executable

---

## 4. User Stories

1. **As the user, I want to create a project for a research topic, so that** related videos and their synthesized knowledge live in one place.
   *Example: create "Local LLM Inference"; a `Documents\SecondBrain\Local LLM Inference\` library folder is created with `theme.css` and an empty `index.html`.*

2. **As the user, I want to paste a YouTube URL into a project, so that** the app ingests it without me finding IDs or metadata.
   *Example: paste `https://youtu.be/abc123`; a video row appears with status "Added" and begins processing.*

3. **As the user, I want the app to fetch the transcript automatically, so that** I never touch subtitle files or scraping tools.
   *Example: within seconds the status moves to "Transcribed" and the video's title, channel, and duration (from Apify) fill in.*

4. **As the user, I want each video summarized into its own page, so that** I can read the gist of one video without watching it.
   *Example: `local-llm-inference/why-quantization-matters.html` appears with a titled summary, tags, and a link back to YouTube.*

5. **As the user, I want the project's `index.html` to be a complete report across all its videos, so that** I get synthesized understanding, not just a list of links.
   *Example: after adding five videos, the report has sections like "Quantization approaches", "Hardware tradeoffs", "Recommended toolchains", each citing the specific video pages it drew from.*

6. **As the user, I want the report to update as I add videos, so that** the library compounds instead of going stale.
   *Example: adding a sixth video (debounced) triggers a regeneration that folds its content into the existing sections.*

7. **As the user, I want to open the library directly in my browser, so that** the knowledge is usable independent of the app.
   *Example: click "Open library ↗" and the on-disk `index.html` opens; it looks identical in style to my hand-built Wild Flowers library.*

8. **As the user, I want to see when a video fails or is parked, so that** I can retry or remove it.
   *Example: a video with no available transcript shows "NoTranscript"; a repeatedly-failing Apify call shows "Parked" with the last error.*

**Technical stories**
9. **As the operator, I want workers to back off and park on repeated failure, so that** a bad video or a down endpoint doesn't spin the CPU or spam the provider.
10. **As the operator, I want LLM endpoint/credentials to be reconfigurable without a restart, so that** I can switch between hosted OpenAI and local Ollama freely.

---

## 5. Core Architecture & Patterns

### High-level approach

A single ASP.NET Core process hosting Blazor Server (Interactive Server) UI plus in-process background workers, all sharing one DI container and one EF Core `DbContext`. The UI injects services directly (no REST layer — there is no trust boundary to cross for a single local user). Background workers advance videos through a fixed pipeline and write OKF-HTML to disk.

### Pipeline

```
Added ──(TranscriptWorker: Apify)──▶ Transcribed ──(SummaryWorker: LLM)──▶ Summarized ──(ReportWorker: LLM)──▶ Published
  │                                       │  writes <video-slug>.html          │  regenerates index.html
  │                                       └──▶ NoTranscript (no subtitles)
  └──▶ Error / Parked (after N retries, exponential backoff)
```

### Solution structure

Two projects (host + UI-free core), mirroring the proven SkipWatch split so `.Core` stays testable and Blazor-free.

```
VideoCortex.slnx
  VideoCortex/                     Blazor Server host — UI, workers, DI composition
    Features/Projects/             Project list, project detail (add box, video table, report links)
    Features/Settings/             Apify token + LLM config + library root
    Workers/                       TranscriptWorker, SummaryWorker, ReportWorker
    wwwroot/okf/                   Bundled OKF templates: theme.css, concept.html, index.html
    Program.cs                     DI wiring, migrations on startup
  VideoCortex.Core/                Domain library (UI-free)
    Entities/                      Project, Video
    Db/                            VideoCortexDbContext, EF Core migrations
    Services/Transcripts/          ITranscriptSource, ApifyTranscriptSource, TranscriptIngestRunner
    Services/Llm/                  OpenAiClient(cache), IVideoSummarizer, IReportSynthesizer
    Services/Library/              IOkfLibraryStore, OkfLibraryStore (disk writer), slug helpers
    Services/Config/               Runtime paths, settings records
  VideoCortex.Tests/               xUnit + FluentAssertions, in-memory SQLite
```

### Key patterns

- **Vertical slice with shared services** (each `Features/<X>` self-contained; cross-cutting services in `.Core`).
- **No repository layer** — services talk to `DbContext` directly; EF Core is the persistence abstraction.
- **No CQRS/MediatR/AutoMapper** — explicit service methods, manual DTO mapping, result-type records for expected outcomes (e.g. `AddVideoResult` with success/duplicate/invalid-url branches).
- **Polling `BackgroundService`** per stage: `TickOnce → sleep(IdlePollSeconds) when idle`, exponential backoff `60s × 2^(n-1)` capped at 1h, park after `MaxRetryAttempts`.
- **Debounced report regeneration** — coalesce rapid adds within `CoalesceDebounceSeconds` before running the (expensive) report LLM call.
- **Atomic disk writes** — temp file + move-overwrite, UTF-8 without BOM, filenames validated against `^[A-Za-z0-9._-]+$`.

---

## 6. Features

### 6.1 Projects

- **Create**: name → slugged folder under the library root; folder seeded with a copy of `theme.css` and an initial `index.html` (empty report shell with valid `okf-meta`).
- **List**: all projects with video counts and last-report-updated time.
- **Detail** (`/projects/{slug}`): project name/description, an **AI instructions** field (per-project guidance threaded into summary + report prompts), a **paste-URL add box**, a **video table** (title, channel, duration, status, per-video "open page" + "remove"), an **"Open library ↗"** link to the on-disk `index.html`, and a **"Rebuild report"** button.
- **Delete**: removes DB rows; disk folder deletion is confirmed explicitly and off by default (the artifact is precious).

### 6.2 Manual video add

- Input parser normalizes YouTube URL forms (`youtube.com/watch?v=`, `youtu.be/`, `shorts/`, bare 11-char ID).
- Duplicate-in-project → surfaced as a no-op with a message.
- Creates a `Video` row with `Status = Added`; the transcript worker picks it up. Title/channel/duration/thumbnail are backfilled from Apify (no YouTube Data API).

### 6.3 Transcript retrieval (Apify)

- `ITranscriptSource.FetchAsync(videoId)` → `ApifyTranscriptSource` calling `streamers~youtube-scraper` `run-sync-get-dataset-items` (one synchronous POST per video).
- Returns transcript text (SRT → `[mm:ss] text`) **plus** metadata: title, channel, description, duration, view/like/comment counts, thumbnail URL.
- No subtitles available → `NoTranscript` (metadata still stored; no summary page written).

### 6.4 Per-video summary (LLM → OKF concept page)

- `IVideoSummarizer.SummarizeAsync(title, channel, transcript)` → OpenAI-compatible chat call with `response_format = json_schema`.
- **Output schema:** `{ "title": string, "description": string, "tags": string[], "body_markdown": string }`.
- App renders `body_markdown` via **Markdig** into the OKF **concept template**: fills `okf-meta` (`type: "Video"`, title, description, tags, `resource` = YouTube URL, `timestamp`), the meta-bar (type + tag chips), and the `<main>` body. Written to `<Project>/<video-slug>.html` with a relative `theme.css` link.

### 6.5 Per-project report (LLM → OKF root index)

- `IReportSynthesizer.SynthesizeAsync(projectName, aiInstructions, videoSummaries[])` → OpenAI-compatible call.
- **Output schema:** `{ "library_description": string, "report_html": string }` where `report_html` is semantic HTML organized into thematic sections, cross-linking the individual video pages inline as **citations** (OKF §8), and ending with a **Sources** section enumerating each video (title + description + link) — preserving OKF's "index lists its concepts" convention.
- App wraps the output in the OKF **root-index template** (carries `okf-meta` `type: "Index"`, `okf_html_version: "0.1"`, title, `library_description`) and writes `<Project>/index.html`.
- Triggered (debounced) when any video reaches `Summarized`, and on **"Rebuild report"**.
- Input is per-video summaries (compact), so many videos fit one context window; oversized projects are a v2 chunking concern (see Risks).

### 6.6 Settings

- Writable page: `Apify:Token`, `Llm:{Model, BaseUrl, ApiKey}`, `Library:RootPath` (default `C:\Users\JasonUser\Documents\SecondBrain`), worker knobs (poll interval, debounce, retry cap, timeouts).
- Config change (endpoint/key/model) takes effect without a process restart via `IOptionsMonitor` + a per-endpoint `HttpClient` cache.

---

## 7. Technology Stack

| Layer | Choice |
|---|---|
| Runtime | .NET 10, C#, nullable enabled, `TreatWarningsAsErrors=true` via root `Directory.Build.props` |
| App framework | Blazor Server (ASP.NET Core, Interactive Server components) |
| Background jobs | In-process `IHostedService` / `BackgroundService` + `PeriodicTimer` (no Quartz/Hangfire) |
| Data | EF Core 10 on `Microsoft.EntityFrameworkCore.Sqlite`, code-first, migrations applied at startup |
| Transcript source | Apify `streamers/youtube-scraper` via `HttpClient` + `IHttpClientFactory` |
| LLM | Any OpenAI-compatible chat endpoint (hosted OpenAI, or Ollama `/v1` locally) via thin `HttpClient` wrapper |
| Markdown → HTML | Markdig |
| Output format | OKF-HTML v0.1 (templates shipped in `wwwroot/okf/`) |
| UI styling | OKF `theme.css` for the on-disk libraries; a minimal in-app stylesheet for the control panel |
| Tests | xUnit + FluentAssertions, in-memory SQLite fixtures |

**No** YouTube Data API, no vector DB / embeddings, no Node/npm build step.

---

## 8. Security & Configuration

- **Auth:** none — single-user localhost app, no trust boundary. Not exposed to a network.
- **Secrets:** Apify token and LLM API key stored via `dotnet user-secrets` in development and a writable local overlay (`%USERPROFILE%\.videocortex\appsettings.Local.json`) at runtime; never committed.
- **Config sections:** `Apify`, `Llm`, `Library`, `TranscriptWorker`, `Summary`, `Report`.
- **Filesystem safety:** the app writes only inside `Library:RootPath\<project-slug>\`; it never modifies pre-existing sibling libraries (e.g. *Wild Flowers*). Slugs and filenames are validated; writes are atomic.
- **Data location:** state DB at `%USERPROFILE%\.videocortex\app.db`; knowledge artifacts under the configured library root.

---

## 9. Data Model

```csharp
class Project {
    int Id;
    string Name;                 // display name, also library folder name (sanitized)
    string Slug;                 // unique, filesystem-safe
    string? Description;
    string? AIInstructions;      // threaded into summary + report prompts
    ProjectStatus Status;        // Idle | Updating | Error
    DateTime? ReportUpdatedAt;
    DateTime CreatedAt;
    ICollection<Video> Videos;   // one-to-many; a video belongs to exactly one project
}

class Video {
    int Id;
    int ProjectId;               // FK; no sharing across projects
    string YoutubeVideoId;
    string? Title, ChannelTitle, ThumbnailUrl, Description;
    int? DurationSeconds;
    long? ViewCount, LikeCount, CommentsCount;
    VideoStatus Status;          // Added | Transcribed | Summarized | Published | NoTranscript | Error
    string? TranscriptText, TranscriptLang;
    string? SummaryTitle, SummaryDescription, SummaryBodyMd;  // LLM output for the concept page
    string? ConceptSlug;         // <video-slug> used for the .html filename
    bool Parked;
    int RetryCount;
    DateTime? NextAttemptAt;
    string? LastError;
    DateTime AddedAt;
    DateTime? TranscribedAt, SummarizedAt, PublishedAt;
}
```

`VideoStatus` (declaration order): `0 Added, 1 Transcribed, 2 Summarized, 3 Published, 4 NoTranscript, 5 Error`.

---

## 10. Success Criteria

**MVP is successful when**, starting from a fresh install:

- [ ] The user can create a project and see a new standalone OKF library folder appear in `Documents\SecondBrain`.
- [ ] Pasting a valid YouTube URL results in a per-video concept page with a readable summary, correct metadata, and a working link back to YouTube — with no manual steps.
- [ ] The project `index.html` is a multi-section synthesized report (not a bare list) that cites the individual video pages and updates when a new video is added.
- [ ] The generated library passes OKF-HTML v0.1 conformance (theme.css at root, every concept has a valid `okf-meta` with non-empty `type`, all internal links relative) and visually matches the existing hand-built libraries.
- [ ] A video with no transcript, or a failing provider, surfaces a clear status and never hangs or spins the pipeline; retries back off and park.
- [ ] Switching the LLM endpoint from hosted OpenAI to a local Ollama `/v1` works without restarting the app.

**Quality indicators:** deterministic, valid HTML every time (no broken pages from LLM output); reports read coherently across 5–20 videos; a full add-to-published cycle for one video completes in well under a minute (network-bound on Apify + LLM).

---

## 11. Implementation Phases

### Phase 1 — Scaffold & foundations
**Goal:** compiling solution, DB, config, bundled templates.
- [ ] `VideoCortex.slnx` with host + `.Core` + tests; `Directory.Build.props`
- [ ] `VideoCortexDbContext`, `Project`/`Video` entities, initial migration, migrate-on-startup
- [ ] Config records + Settings binding; user-secrets + overlay
- [ ] Bundle OKF `theme.css` + concept/index templates in `wwwroot/okf/`
**Validation:** app builds & runs; DB file created; templates present.

### Phase 2 — Projects
**Goal:** create/list/detail/delete projects; library folder scaffolding.
- [ ] Project CRUD services + pages
- [ ] `OkfLibraryStore.CreateLibrary` — folder + `theme.css` copy + empty `index.html`
- [ ] Slug generation + collision handling
**Validation:** creating a project produces a valid, browsable (empty) OKF library on disk.

### Phase 3 — Manual add
**Goal:** paste a URL, get a persisted video row.
- [ ] YouTube input parser + tests
- [ ] `AddVideoByUrl` (duplicate/invalid handling) → `Video(Status=Added)`
- [ ] Video table UI with live status
**Validation:** pasted URLs create rows; bad input is rejected cleanly.

### Phase 4 — Transcript
**Goal:** Apify transcript + metadata.
- [ ] `ApifyTranscriptSource` + `TranscriptIngestRunner` + `TranscriptWorker`
- [ ] Backoff/park logic; `NoTranscript` handling
**Validation:** a real video reaches `Transcribed` with metadata; a subtitle-less video reaches `NoTranscript`.

### Phase 5 — Summary → concept page
**Goal:** per-video OKF concept page.
- [ ] OpenAI-compatible client + cache; `IVideoSummarizer` (json_schema)
- [ ] `SummaryWorker`; Markdig render into OKF concept template; write `<slug>.html`
**Validation:** each summarized video yields a valid OKF concept page linked from the app.

### Phase 6 — Report → root index
**Goal:** synthesized, self-updating project report.
- [ ] `IReportSynthesizer` (json_schema); `ReportWorker` (debounced)
- [ ] Write root `index.html` from OKF index template; "Rebuild report" + "Open library" UI
- [ ] Remove-video → delete concept page + regenerate report
**Validation:** report is multi-section, cites video pages, updates on add/remove, and passes OKF conformance.

### Phase 7 — Settings & resilience polish
**Goal:** operator-grade configuration and error surfacing.
- [ ] Settings page (Apify, LLM, library root, worker knobs) with hot reload
- [ ] Error/park surfacing + per-video retry action
**Validation:** endpoint swap works without restart; parked items are visible and retriable.

---

## 12. Future Considerations

- **Chunked report synthesis** (map-reduce) for large projects beyond a single context window.
- **Full-text search** across all libraries (SQLite FTS5).
- **In-app page editing** with an "AI-edited vs user-edited" guard so regenerations don't clobber manual edits.
- **Global root index** across all projects in `SecondBrain`.
- **Additional sources** (podcasts, articles) feeding the same OKF pipeline.
- **Single-file self-contained packaging** + launcher.
- **Optional YouTube Data API** for richer/faster metadata.

---

## 13. Risks & Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| **LLM emits invalid/unsafe HTML** for the report or concept body | Broken or inconsistent pages | Constrain output: concept body is Markdown→Markdig (never raw HTML); report is wrapped in a fixed template; validate/parse before writing; keep the previous good file on failure. |
| **Large project overflows LLM context** during report synthesis | Failed or truncated reports | MVP synthesizes from compact per-video summaries (not transcripts); flag oversized projects; defer map-reduce chunking to v2. |
| **Apify cost / rate limits / transcript gaps** | Videos stall at Added/NoTranscript | Backoff + park + clear status; per-video retry; single sync call per video; surface provider errors verbatim. |
| **App writes into or corrupts an existing hand-built library** | Loss of user's curated data | Writes confined to `RootPath\<slug>\`; app-created folders tracked in DB; never touch unknown sibling folders; atomic writes; folder deletion opt-in only. |
| **OKF drift** — generated libraries stop matching hand-built ones | Inconsistent look/format | Ship the exact template `theme.css`; conformance checks in tests (theme.css present, `okf-meta` valid, `type` non-empty, links relative). |
| **Regeneration churn** on rapid adds | Wasted LLM spend, thrash | Debounce report regeneration; coalesce bursts; only regenerate on `Summarized` transitions or explicit rebuild. |

---

## 14. Appendix

### Relationship to SkipWatch
Video Cortex is a clean-room, standalone descendant of **SkipWatch** (`C:\Repos\SkipWatch`). It reuses SkipWatch's architectural patterns (two-project split, polling workers with backoff/parking, OpenAI-compatible LLM client with per-endpoint caching, Apify transcript source) but shares **no code or dependency** with it and implements only the Project → report slice. It drops: channels, topics, discovery cron, quota, triage/Library/Pass, guides, MCP, and Ops.

### Output format
OKF-HTML v0.1 — see the user's `Documents\SecondBrain\okf\SPEC.md` and the existing reference libraries *Wild Flowers* and *Sci Fi Writing*.

### Key runtime paths
- State DB: `%USERPROFILE%\.videocortex\app.db`
- Config overlay: `%USERPROFILE%\.videocortex\appsettings.Local.json`
- Libraries: `C:\Users\JasonUser\Documents\SecondBrain\<Project Name>\`

### Repository
`C:\Repos\VideoCortex` — PR-based workflow (branch + PR, never commit directly to `main`).
