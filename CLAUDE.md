# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Video Cortex is a single-user, locally installed **.NET 10 Blazor Server** application that turns YouTube videos into a compounding, browsable knowledge library. The user creates a **Project**, pastes in YouTube video URLs, and the app fetches each video's transcript, summarizes it with an LLM, and maintains a per-project **report** synthesizing everything the project's videos cover.

The durable output is **OKF-HTML** (Open Knowledge Format, HTML profile) written to disk under `Documents\SecondBrain\<Project Name>\` — each project is a standalone OKF library (one `theme.css`, a root `index.html` report, and one concept page per video) that opens in any browser and outlives the app. Video Cortex is a deliberately simplified, standalone descendant of **SkipWatch** (`C:\Repos\SkipWatch`); it reuses SkipWatch's patterns but shares no code with it.

Core pipeline, end-to-end: **Paste URL → Added → Transcribed (Apify) → Summarized (LLM → concept page) → Published (LLM → project report).**

See `README.md` for setup/usage and `docs/prd.md` for the authoritative spec. Phase plans live in `docs/phases/`; `docs/progress.md` is the generated task index.

## Repository Location & Layout

The git repository is this directory: `C:\Repos\VideoCortex` (initialized directly here — there is **no** nested project folder, unlike SkipWatch/SeedForge). Default branch: `main`. No remote is configured yet — add `origin` and authenticate `gh` before pushing or opening PRs.

Solution file: `VideoCortex.slnx`. Projects:

- `VideoCortex/VideoCortex.csproj` — Blazor Server host (UI, workers, DI composition).
- `VideoCortex.Core/VideoCortex.Core.csproj` — UI-free domain library.
- `VideoCortex.Tests/VideoCortex.Tests.csproj` — xUnit test project.

## Build & Test

```bash
dotnet build VideoCortex.slnx
dotnet test  VideoCortex.slnx
dotnet run   --project VideoCortex/VideoCortex.csproj
dotnet format VideoCortex.slnx          # formatting; no separate linter configured
```

`Directory.Build.props` enforces `TreatWarningsAsErrors=true`, `Nullable=enable`, `net10.0` across all projects — the build fails on unused usings and nullable warnings.

### EF Core Migrations

A single context, `VideoCortexDbContext`. Migrations live in `VideoCortex.Core/Db/Migrations/` and are applied automatically at startup via `Database.Migrate()` in `Program.cs` — no manual `database update` needed for normal runs.

```bash
dotnet ef migrations add <Name> --project VideoCortex.Core --startup-project VideoCortex --output-dir Db/Migrations
```

## Architecture

Vertical slice with shared services. **No MediatR, no repository pattern, no CQRS, no AutoMapper** — services talk to `VideoCortexDbContext` directly; use explicit service methods, manual DTO mapping, and result-type records (e.g. `AddVideoResult` with Added/DuplicateInProject/InvalidUrl branches) instead of exception-driven control flow. The two-project split keeps `VideoCortex.Core` UI-free so background workers can reference it without dragging in Blazor.

Key locations:

- **`VideoCortex/`** — Blazor Server host.
  - `Features/Projects/` — Project list, detail (add box, video table, report links), CRUD service.
  - `Features/Settings/` — Apify token, LLM config, library root, worker knobs.
  - `Workers/` — `TranscriptWorker`, `SummaryWorker`, `ReportWorker` (hosted `BackgroundService`s).
  - `wwwroot/okf/` — Bundled OKF templates (`theme.css`, `concept.html`, `index.html`) copied into each project library.
- **`VideoCortex.Core/`** — Domain library.
  - `Entities/` — `Project`, `Video`, enums (`ProjectStatus`, `VideoStatus`).
  - `Db/` — `VideoCortexDbContext` (+ migrations).
  - `Services/Transcripts/` — `ITranscriptSource`, `ApifyTranscriptSource`, ingest runner.
  - `Services/Llm/` — OpenAI-compatible client + cache, summarizer, report synthesizer.
  - `Services/Library/` — `IOkfLibraryStore` (disk writer for OKF pages).
  - `Services/Config/` — runtime paths + strongly-typed settings records.
- **`VideoCortex.Tests/`** — xUnit + FluentAssertions, in-memory SQLite fixtures.

### Key Patterns

- **Pipeline Stages On `Video`** — `Added → Transcribed → Summarized → Published`, plus `NoTranscript` / `Error`. Each stage is driven by a polling background worker. Enum member order is the persisted int contract — never reorder.
- **Background Processing** — each worker is a `BackgroundService` polling loop (`TickOnce → sleep(IdlePollSeconds)` when idle), with exponential backoff (`60s × 2^(n-1)`, capped at 1h) and parking after `MaxRetryAttempts`. The report worker additionally debounces via a per-project dirty timestamp + `CoalesceDebounceSeconds`.
- **OKF-HTML Output** — the LLM returns structured data (Markdown body / inner HTML), never full pages; the OKF templates + Markdig wrap it deterministically so every page is valid and uniformly themed. All internal links and the `theme.css` link are **relative**.
- **External Service Slots** — Apify for transcripts + metadata (no YouTube Data API); any OpenAI-compatible LLM endpoint via `Model` / `BaseUrl` / `ApiKey` (blank `BaseUrl` → hosted OpenAI; point at Ollama `/v1` for local). A per-endpoint `HttpClient` cache rebuilds on config change via `IOptionsMonitor`, so endpoint/key/model changes take effect without a restart.
- **Filesystem Safety** — the app writes only inside `Library:RootPath\<project folder>\`. It must **never** modify pre-existing sibling libraries in `SecondBrain` (e.g. `Wild Flowers`, `Sci Fi Writing`). Writes are atomic (temp file + move), UTF-8 without BOM; filenames validated against `^[A-Za-z0-9._-]+$`.

## Dependency Injection Rules

- `OpenAiClientCache` (thread-safe, holds pooled `HttpClient`s) → **singleton**.
- `ApifyTranscriptSource` → **typed `HttpClient`** (`AddHttpClient<…>`; base address + timeout from options).
- `VideoCortexDbContext`, ingest runners, request-scoped services → **scoped**.
- Slug helpers, the YouTube input parser, SRT converter → **static** (no DI).
- ⚠️ Never inject a **scoped** service (e.g. `DbContext`) into a **singleton** (e.g. a worker) — open a fresh scope with `IServiceScopeFactory` inside each tick (captive-dependency hazard).

## Configuration

All settings live in `VideoCortex/appsettings.json` under the `Apify`, `Llm`, `Library`, `TranscriptWorker`, `Summary`, and `Report` sections (bound to settings records). Non-secret defaults ship in source; **secrets are blank in source** and supplied via user-secrets in development:

```bash
cd VideoCortex
dotnet user-secrets set "Apify:Token" "apify_api_..."
dotnet user-secrets set "Llm:ApiKey"  "sk-..."
```

At runtime, non-secret and secret values are written to a hot-reloaded overlay at `%USERPROFILE%\.videocortex\appsettings.Local.json` (layered with `reloadOnChange:true`), editable from the Settings page. User-secrets is **not** hot-reloaded; the overlay is. Never place secrets in the committed `appsettings.json`.

Runtime paths:

- State DB: `%USERPROFILE%\.videocortex\app.db` (SQLite).
- Config overlay: `%USERPROFILE%\.videocortex\appsettings.Local.json`.
- Libraries: `C:\Users\JasonUser\Documents\SecondBrain\<Project Name>\` (default `Library:RootPath`).

## Conventions

- Async for all I/O; explicit return types.
- **Interfaces only at the network/service boundary** (transcript source, summarizer, report synthesizer, library store) — everything else concrete; pure helpers are `static`.
- `ILogger<T>` per class — **never `Console.WriteLine`**.
- Options pattern for config — no scattered `Environment.GetEnvironmentVariable`; resolve paths via `VideoCortexPaths`, never hardcode `C:\Users\JasonUser`.
- A brief XML doc comment atop every file/class.
- **No Secrets In Source Or `appsettings.json`** — user-secrets (dev) or the local overlay (runtime) only.
- **Title Case Buttons** — every button label uses Title Case (e.g. "Add Video", "Rebuild Report", "Open Library", "Retry"). Minor words (a, an, the, and, or, to, vs, in, of, …) stay lowercase unless they are the first word. Applies to `<button>` and button-styled `<a class="btn">` labels.
- **OKF-HTML Conformance** — generated libraries must stay OKF-HTML v0.1 conformant and visually match the hand-built reference libraries; `theme.css` is shipped verbatim and never edited per-page. See `Documents\SecondBrain\okf\SPEC.md`.

## Testing

- **xUnit** with **FluentAssertions**; **in-memory SQLite** (keep one connection open per fixture for the test's lifetime — `:memory:` vanishes when the connection closes).
- **No network in tests** — mock `HttpMessageHandler` for the Apify and LLM clients; feed canned payloads. A real Apify/LLM call is manual-only and never a CI gate.
- Disk writers (concept page, report) are tested against a temp directory, asserting OKF conformance (valid `okf-meta` JSON with non-empty `type`, relative `theme.css` link, `<h1>` present).
- Name tests for behavior, e.g. `MethodName_Condition_ExpectedResult`.

## Commit Authorship

Do not add Claude as a contributor, author, or `Co-Authored-By` on any commit **or pull request** in this repository, and do not add "Generated with Claude Code" or any similar attribution to commit messages or PR bodies. All commits and PRs are authored solely by the user.
