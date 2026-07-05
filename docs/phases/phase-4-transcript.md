# Phase 4: Transcript

The following plan should be complete, but it is important that you validate documentation and codebase patterns and task sanity before you start implementing.

Pay special attention to naming of existing utils, types, and models. Import from the right files.

## Phase Description

This phase adds the first background pipeline stage to **Video Cortex**: automatic transcript **and metadata** retrieval from Apify for every `Video` row a user has pasted (`Status = Added`). A single seam `ITranscriptSource` is implemented by `ApifyTranscriptSource` — a thin `HttpClient` (via `IHttpClientFactory`) that makes one synchronous POST to Apify's `streamers~youtube-scraper` `run-sync-get-dataset-items` endpoint per video and returns the scraped object inline. From that one call the app extracts the transcript (SRT → `[mm:ss] text`) **plus** title, channel, description, duration, view/like/comment counts and thumbnail URL. A per-video `TranscriptIngestRunner` (in `.Core`) applies the result to the `Video` row and drives the status transition, and a `TranscriptWorker` (a hosted `BackgroundService` in the host) polls the queue and hands each due row to the runner.

On success with subtitles the row backfills its metadata columns and moves `Added → Transcribed`; on success **without** subtitles it still stores metadata but moves `Added → NoTranscript` (so no summary is attempted later); on failure it applies exponential backoff (`60s × 2^(n-1)`, capped at 1h) and parks after `MaxRetryAttempts`. There is **no YouTube Data API** — Apify is the sole metadata source (PRD §7).

This phase builds on the Phase 1 foundations (`VideoCortexDbContext`, the `Video` entity with its pipeline-state columns, the `ApifySettings` / `TranscriptWorkerSettings` config records, migrate-on-startup, config overlay) and on the Phase 2/3 slice that creates `Project` rows and produces `Video(Status = Added)` rows from a pasted URL. It ships **no UI** — the video-table status column added in Phase 3 will simply start showing `Transcribed` / `NoTranscript` / `Error` as rows advance.

This is a greenfield repository. There is **no existing Video Cortex transcript code**; the reference implementation for every pattern is **SkipWatch** at `C:\Repos\SkipWatch\SkipWatch`. Read those files to copy conventions, then write clean, minimal equivalents — do **not** add a project reference to SkipWatch and do **not** copy its channel/quota/`ActivityEntry`/`JobEventBus` machinery (Video Cortex has none of those).

## User Stories

As the single local user
I want the app to fetch each pasted video's transcript and metadata automatically
So that within seconds of pasting a URL the row shows its real title, channel and duration and is ready to be summarized — without me touching subtitle files or scraping tools.

As the single local user
I want a video that genuinely has no captions to be marked clearly rather than retried forever
So that I can see it was ingested (metadata present) but will never get a summary, and the pipeline doesn't spin on it.

As the operator
I want the transcript worker to back off and park on repeated failure
So that a bad video, a missing Apify token, or a down provider doesn't spin the CPU or spam a paid API.

## Problem Statement

After Phase 3 a pasted URL produces only a bare `Video(Status = Added, YoutubeVideoId = …)` row with no title, no transcript, and no metadata — it just sits there. Nothing turns that ID into content. Video Cortex needs a deterministic, resilient stage that calls Apify once per video, populates the row, and moves it forward (or parks it) so the summary stage in Phase 5 has a transcript and metadata to work with.

## Solution Statement

Introduce the transcript stage as three small, independently testable pieces mirroring SkipWatch's proven shape:

1. **`ITranscriptSource` + `Transcript` record** (`.Core`) — a one-method seam and its result payload carrying `Success` / `HasTranscript`, the transcript text + language, the Apify metadata (title, channel, description, duration, view/like/comment counts, thumbnail URL), and an error message.
2. **`ApifyTranscriptSource`** (`.Core`) — the `HttpClient`-based implementation: one `run-sync-get-dataset-items` POST, SRT → `[mm:ss]` conversion via a small `SrtConverter`, ISO-8601 duration parsing, clean failure (never an uncaught throw) when the token is missing or Apify errors.
3. **`TranscriptIngestRunner`** (`.Core`, per-video logic) + **`TranscriptWorker`** (host `BackgroundService`, polling loop). The runner reuses the Phase 1 `Video` pipeline-state columns (`Status`, `Parked`, `RetryCount`, `NextAttemptAt`, `LastError`) to apply the result, backfill metadata, and transition status. The worker polls `Status == Added && !Parked && (NextAttemptAt == null || <= now)`, hands each row to the runner, then idles.

Everything is exercised by tests that never hit the network: `ApifyTranscriptSource` against a mocked `HttpMessageHandler` fed a captured Apify JSON payload; `SrtConverter` directly; the runner against in-memory SQLite with a fake `ITranscriptSource` (success-with-transcript, success-no-transcript, transient failure → retry/backoff, repeated failure → park).

## Phase Metadata

**Phase Type**: New Capability (first pipeline stage)
**Estimated Complexity**: Medium
**Primary Systems Affected**: `.Core` transcript services, host background workers, DI composition, config wiring, test suite
**Dependencies**: Phases 1–3. From Phase 1: `VideoCortexDbContext`, `Video` entity (pipeline-state columns + metadata columns), `VideoStatus` enum, `ApifySettings` + `TranscriptWorkerSettings` config records + `appsettings.json` sections, migrate-on-startup, config overlay, `IHttpClientFactory` availability. From Phases 2–3: `Project` rows and the `AddVideoByUrl` path that creates `Video(Status = Added)` rows. External: Apify `streamers/youtube-scraper` actor (a real end-to-end call requires a valid `Apify:Token` and costs money — **optional / manual only, never a CI gate**).

---

## CONTEXT REFERENCES

### Relevant Codebase Files IMPORTANT: YOU MUST READ THESE FILES BEFORE IMPLEMENTING!

These are **SkipWatch reference files** (a separate repo) — read them for patterns to mirror, then write minimal Video Cortex equivalents. Do not reference or depend on the SkipWatch projects. Cited line ranges are from SkipWatch at time of writing.

- `C:\Repos\SkipWatch\SkipWatch\SkipWatch.Core\Services\Interfaces\ITranscriptSource.cs` (lines 1–15) — Why: the exact one-method seam (`Task<Transcript> FetchAsync(string videoId, CancellationToken ct = default)`) and the "keep the provider surface narrow" rationale. Mirror verbatim in spirit; namespace becomes `VideoCortex.Core.Services.Transcripts`.
- `C:\Repos\SkipWatch\SkipWatch\SkipWatch.Core\Services\Transcripts\Transcript.cs` (lines 1–20) — Why: the result record shape. Note the SkipWatch record does NOT carry `Title`/`ChannelTitle`; Video Cortex adds `Title` and `ChannelTitle` fields because there is no YouTube Data API to fill them elsewhere (PRD §6.2 "Title/channel/duration/thumbnail are backfilled from Apify").
- `C:\Repos\SkipWatch\SkipWatch\SkipWatch.Core\Services\Transcripts\ApifyTranscriptSource.cs` (lines 18–170) — Why: the whole implementation to mirror — `run-sync-get-dataset-items` endpoint constant (line 21–22), request DTO with `startUrls`/`downloadSubtitles`/`preferAutoGeneratedSubtitles`/`subtitlesLanguage` (lines 140–151), missing-token guard returning a `Failure(...)` rather than throwing (lines 53–54), non-2xx handling reading the body into the error (lines 74–80), inline deserialize of `List<ApifyScraperItem>` (lines 82–87), `SelectBestSubtitle` preference order (lines 119–133), timeout override on the injected `HttpClient` (line 45), and the `TaskCanceledException`/general `catch` that convert to `Failure(...)` (lines 107–116).
- `C:\Repos\SkipWatch\SkipWatch\SkipWatch.Core\Services\Utilities\SrtConverter.cs` (lines 6–46, method `ConvertSrtToPrdFormat`) — Why: the SRT → `[mm:ss] text` converter. Copy `ConvertSrtToPrdFormat` (splits on blank lines, regex `(\d+):(\d+):(\d+),\d+` on the timing line, folds `hours*60+minutes` into `mm`, joins the text lines). Video Cortex does not need `ConvertSrtToSimpleFormat`.
- `C:\Repos\SkipWatch\SkipWatch\SkipWatch.Core\Services\YouTube\DurationParser.cs` (lines 3–21) — Why: the ISO-8601 (`PT4M13S`) → seconds parser (`XmlConvert.ToTimeSpan`, returns 0 on failure). Copy this as a small `.Core` utility (Video Cortex has no `YouTube` folder — place it under `Services/Transcripts/` or `Services/Utilities/`). Apify's `duration` field is what feeds it.
- `C:\Repos\SkipWatch\SkipWatch\SkipWatch.Core\Services\Transcripts\ApifySettings.cs` (lines 3–35) — Why: the settings shape (`Token`, `RunTimeoutSeconds=300`, `PreferredLanguage="en"`, `PreferAutoGenerated=true`). Phase 1 already defines `ApifySettings` — confirm those fields exist and add any missing; drop SkipWatch's `CostPerVideoUsd` (no Ops dashboard here).
- `C:\Repos\SkipWatch\SkipWatch\SkipWatch.Core\Services\Transcripts\TranscriptIngestRunner.cs` (lines 13–172) — Why: the per-video logic to mirror — backoff constants `BaseBackoff = 60s`, `MaxBackoff = 1h` (lines 15–16), the `try/catch` around `FetchAsync` converting a throw into a failed `Transcript` (lines 54–67), the failure path (bump `RetryCount`, set `LastError`, park at `>= MaxRetryAttempts` else set `NextAttemptAt` via `60s × 2^(RetryCount-1)` capped at 1h, lines 73–118), the success path (overwrite cheap fields only when non-null, set transcript/status, reset retry state, lines 120–147), and the `HasTranscript` branch choosing `Transcribed` vs `NoTranscript` (lines 128–142). **Strip** the SkipWatch-only bits: `ActivityEntry` logging (Video Cortex has no `Activity` table), `JobEventBus`/`PublishSafe` (no event bus in the MVP), and `DiscoverySettings` (use `TranscriptWorkerSettings.MaxRetryAttempts` instead).
- `C:\Repos\SkipWatch\SkipWatch\SkipWatch.Core\Services\Transcripts\ITranscriptIngestRunner.cs` (lines 5–26) — Why: the runner interface + `TranscriptIngestOutcome { Transcribed, NoTranscript, Retry, Parked }` enum + `TranscriptIngestResult(Outcome, RetryCount, Error, ElapsedMs)` record. Mirror as-is.
- `C:\Repos\SkipWatch\SkipWatch\SkipWatch\Services\Workers\TranscriptWorker.cs` (lines 9–81) — Why: the polling `BackgroundService` shape — `ExecuteAsync` loop calling `TickOnceAsync`, `didWork` gating the idle `Task.Delay(IdlePollSeconds)`, `OperationCanceledException` breaking cleanly, and `TickOnceAsync` opening a scope, querying the due row (`Status == …Added`, `!Parked`, `NextAttemptAt == null || <= now`, ordered by `NextAttemptAt` then the added timestamp), and delegating to the runner. Adapt the status filter from `Discovered` to `Added` and the order-by from `IngestedAt` to `AddedAt`. Drop the `Concurrency` log field if Phase 1's `TranscriptWorkerSettings` has no `Concurrency` knob.
- `C:\Repos\SkipWatch\SkipWatch\SkipWatch.Tests\Services\Transcripts\TranscriptIngestRunnerTests.cs` (lines 14–273) — Why: the exact test shape to mirror — in-memory keep-alive SQLite (`Data Source=:memory:` opened once, `db.Database.Migrate()`), a `FakeTranscriptSource : ITranscriptSource` with a `Queue<Transcript>` + `ThrowOnNext`, and the six scenarios (success-with-transcript, success-no-transcript, cheap-fields-not-overwritten-when-null, failure→retry+backoff≈60s, failure-at-max→park, backoff doubling/cap, thrown→treated-as-transient). Adapt seeding to Video Cortex's `Project`→`Video` model (no `Channel`).
- `C:\Repos\SkipWatch\SkipWatch\SkipWatch\Program.cs` (lines 45, 54–56, 73–74, 80) — Why: DI registration to mirror — `Configure<TranscriptWorkerSettings>(GetSection("TranscriptWorker"))`, `AddHttpClient<ITranscriptSource, ApifyTranscriptSource>()` (typed client, `IHttpClientFactory`-managed lifetime), `AddScoped<ITranscriptIngestRunner, TranscriptIngestRunner>()`, `AddHostedService<TranscriptWorker>()`.

### Video Cortex files this phase touches (from Phase 1)

Confirm these exist and match the assumptions below before implementing; if a name differs, adapt to the real one rather than the assumed one.

- `C:\Repos\VideoCortex\VideoCortex.Core\Entities\Video.cs` — pipeline-state columns (`Status`, `Parked`, `RetryCount`, `NextAttemptAt`, `LastError`) + metadata columns (`Title`, `ChannelTitle`, `ThumbnailUrl`, `Description`, `DurationSeconds`, `ViewCount`, `LikeCount`, `CommentsCount`), transcript columns (`TranscriptText`, `TranscriptLang`), and timestamps (`AddedAt`, `TranscribedAt`). Note: PRD §9 lists no `ParkedAt`/`HasTranscript` columns — do **not** assume them; use `Parked` (bool) and derive "has transcript" from `Status == Transcribed` / non-null `TranscriptText`.
- `C:\Repos\VideoCortex\VideoCortex.Core\Entities\Enums.cs` — `VideoStatus { Added, Transcribed, Summarized, Published, NoTranscript, Error }` (declaration order = persisted int; do not reorder).
- `C:\Repos\VideoCortex\VideoCortex.Core\Db\VideoCortexDbContext.cs` — `DbSet<Video>`; the `Video.Status` index used by the worker query.
- `C:\Repos\VideoCortex\VideoCortex.Core\Services\Config\Settings.cs` — `ApifySettings`, `TranscriptWorkerSettings` records (Phase 1). Confirm `TranscriptWorkerSettings` has `IdlePollSeconds` and `MaxRetryAttempts`.
- `C:\Repos\VideoCortex\VideoCortex\Program.cs` — DI composition root to extend.
- `C:\Repos\VideoCortex\VideoCortex\appsettings.json` — the `Apify` and `TranscriptWorker` sections (Phase 1).

### New Files to Create

- `C:\Repos\VideoCortex\VideoCortex.Core\Services\Transcripts\ITranscriptSource.cs` — the seam.
- `C:\Repos\VideoCortex\VideoCortex.Core\Services\Transcripts\Transcript.cs` — the result record (with `Title` + `ChannelTitle`).
- `C:\Repos\VideoCortex\VideoCortex.Core\Services\Transcripts\ApifyTranscriptSource.cs` — the `HttpClient` implementation + private Apify request/response DTOs.
- `C:\Repos\VideoCortex\VideoCortex.Core\Services\Transcripts\SrtConverter.cs` — SRT → `[mm:ss] text` (static).
- `C:\Repos\VideoCortex\VideoCortex.Core\Services\Transcripts\DurationParser.cs` — ISO-8601 → seconds (static).
- `C:\Repos\VideoCortex\VideoCortex.Core\Services\Transcripts\ITranscriptIngestRunner.cs` — interface + `TranscriptIngestOutcome` + `TranscriptIngestResult`.
- `C:\Repos\VideoCortex\VideoCortex.Core\Services\Transcripts\TranscriptIngestRunner.cs` — per-video logic.
- `C:\Repos\VideoCortex\VideoCortex\Workers\TranscriptWorker.cs` — hosted polling loop (namespace `VideoCortex.Workers`).
- `C:\Repos\VideoCortex\VideoCortex.Tests\Services\Transcripts\SrtConverterTests.cs`.
- `C:\Repos\VideoCortex\VideoCortex.Tests\Services\Transcripts\ApifyTranscriptSourceTests.cs` (mocked `HttpMessageHandler`).
- `C:\Repos\VideoCortex\VideoCortex.Tests\Services\Transcripts\TranscriptIngestRunnerTests.cs` (in-memory SQLite + fake source).
- `C:\Repos\VideoCortex\VideoCortex.Tests\TestData\apify-scraper-item.json` — a captured/synthetic Apify `run-sync-get-dataset-items` payload (one item, with an `srt` subtitle + metadata) used by the source test. Keep it small and offline.

### Relevant Documentation YOU SHOULD READ THESE BEFORE IMPLEMENTING!

- [Typed `HttpClient` with `IHttpClientFactory`](https://learn.microsoft.com/en-us/dotnet/core/extensions/httpclient-factory) — `AddHttpClient<TInterface, TImpl>()`, injected `HttpClient`, factory-managed lifetime. Why: `ApifyTranscriptSource` registration.
- [Unit-testing `HttpClient` with a custom `HttpMessageHandler`](https://learn.microsoft.com/en-us/dotnet/core/extensions/httpclient-factory#use-httpclientfactory-in-a-console-app) plus the general pattern of a fake handler returning a canned `HttpResponseMessage`. Why: offline `ApifyTranscriptSource` tests (no network).
- [`BackgroundService` / hosted services](https://learn.microsoft.com/en-us/dotnet/core/extensions/workers) and [`IServiceScopeFactory` scoped-in-singleton pattern](https://learn.microsoft.com/en-us/dotnet/core/extensions/scoped-service) — Why: the worker is a singleton that must open a DI scope per tick to use the scoped `DbContext` + runner.
- [Apify `run-sync-get-dataset-items`](https://docs.apify.com/api/v2/act-run-sync-get-dataset-items-post) — the synchronous run endpoint returning dataset items inline. Why: the actor call `ApifyTranscriptSource` makes. **Do not call it in tests.**
- [System.Text.Json deserialization](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/how-to) + `[JsonPropertyName]` — Why: the Apify DTOs.
- OKF/PRD context: `C:\Repos\VideoCortex\docs\prd.md` §6.3 (Transcript retrieval), §7 (no YouTube Data API), §9 (Video data model), §11 Phase 4, §13 (Apify cost/backoff risk). Why: authoritative scope.
- `C:\Repos\VideoCortex\docs\phases\phase-1-foundation.md` — Why: the config records, entity columns, DI wiring, and test-fixture conventions this phase depends on. Match its structure and tone.

### Patterns to Follow

**Naming**: PascalCase types/methods, `I`-prefixed interfaces, `record` for the result/DTO payloads, one primary type per file, enums in declaration order (persisted as ints — never reorder). Namespaces: `.Core` code → `VideoCortex.Core.Services.Transcripts`; the worker → `VideoCortex.Workers`.

**Fail-clean, never-throw across the seam**: `ApifyTranscriptSource.FetchAsync` must return `Transcript(Success:false, …, ErrorMessage:…)` for a missing token, a non-2xx response, an empty dataset, a timeout, or any unexpected exception — it must not let those escape (mirror SkipWatch lines 50–116). The runner additionally wraps the call in its own `try/catch` so even a bug that throws becomes a transient failure (SkipWatch lines 54–67). Only a caller-driven `OperationCanceledException` propagates.

**Backoff + park** (mirror SkipWatch runner exactly): on failure bump `RetryCount`, set `LastError`; if `RetryCount >= MaxRetryAttempts` set `Parked = true`, `NextAttemptAt = null`; else `NextAttemptAt = UtcNow + min(1h, 60s × 2^(RetryCount-1))`. Status stays `Added` on failure (the worker re-queries it after `NextAttemptAt`).

**Metadata backfill on success**: overwrite a `Video` metadata column **only when Apify returned a non-null value** (mirror SkipWatch lines 120–126), so a partial Apify response never nulls out a good value. `Title`/`ChannelTitle` follow the same "only if non-null" rule.

**Status transition on success**: `HasTranscript` → set `TranscriptText`/`TranscriptLang`/`TranscribedAt`, `Status = Transcribed`; else `Status = NoTranscript` (metadata still saved, no transcript). Either way reset `RetryCount = 0`, `LastError = null`, `NextAttemptAt = null`.

**Worker = scoped-per-tick**: the `BackgroundService` is a singleton; open `_scopeFactory.CreateScope()` inside `TickOnceAsync`, resolve the scoped `DbContext` + runner there, and let the scope dispose per tick. `TickOnceAsync` returns `true` when it processed a row (loop immediately) and `false` when the queue was empty (loop sleeps `IdlePollSeconds`).

**Warnings-as-errors**: everything compiles clean under `TreatWarningsAsErrors=true` — no unused usings, no nullable warnings. `Directory.Build.props` from Phase 1 enforces this.

**No network in tests**: every test uses a fake `ITranscriptSource` or a fake `HttpMessageHandler`. Nothing in the test suite or in any VALIDATE step touches Apify.

---

## IMPLEMENTATION PLAN

**Rendering**: Milestones — the phase has natural sub-layers (seam + result → Apify source + converters → runner → worker/DI → tests) worth surfacing.

**Rationale**: 8 tasks across the seam/source/runner/worker layers with cross-layer integration (the worker must resolve the runner which must resolve the source, all wired in `Program.cs`). Milestone checkpoints prove integration (e.g. "the source deserializes a captured payload", "the runner transitions status") beyond each task's isolated VALIDATE.

Tasks execute one at a time, top to bottom. Task numbering is contiguous across milestones.

### Milestone 1: Seam, result, and Apify source

`ITranscriptSource` + `Transcript` exist; `ApifyTranscriptSource` compiles and (via its converters) turns a captured Apify payload into a `Transcript`.

**Validation checkpoint**: `dotnet build VideoCortex.Core` is clean, and the source-level unit tests added in Task 4 pass against the captured JSON with no network.

#### Task 1: Transcript seam + result record

Create the provider seam and the payload it returns.

- **IMPLEMENT**: `ITranscriptSource` with `Task<Transcript> FetchAsync(string videoId, CancellationToken ct = default)`. `Transcript` record with `bool Success`, `string? TranscriptText`, `string? TranscriptLang`, `bool HasTranscript`, `string? Title`, `string? ChannelTitle`, `string? Description`, `int? DurationSeconds`, `long? ViewCount`, `long? LikeCount`, `long? CommentsCount`, `string? ThumbnailUrl`, `string? ErrorMessage`. Add a private/internal static `Failure(string message)` helper convention (or a `static` factory) so callers build failures without listing every null.
- **PATTERN**: `SkipWatch.Core\Services\Interfaces\ITranscriptSource.cs` (1–15); `SkipWatch.Core\Services\Transcripts\Transcript.cs` (1–20). Add `Title` + `ChannelTitle` (SkipWatch's record omits them because it has a YouTube Data API path; Video Cortex does not).
- **IMPORTS**: `System`, `System.Threading`, `System.Threading.Tasks`; namespace `VideoCortex.Core.Services.Transcripts`.
- **GOTCHA**: Keep the field order stable and documented — the runner and every test construct `Transcript` positionally. Choose the order once (recommend the order listed above) and keep it.
- **VALIDATE**: `cd /c/Repos/VideoCortex && dotnet build VideoCortex.Core` exits 0.

#### Task 2: SRT + duration converters

Port the two small deterministic utilities.

- **IMPLEMENT**: `SrtConverter` (static) with `ConvertSrtToPrdFormat(string srt) → [mm:ss] text` lines (split on blank-line boundaries, regex `(\d+):(\d+):(\d+),\d+` on the timing line, `mm = hours*60+minutes`, join wrapped caption lines, skip empties). `DurationParser` (static) with `ParseToSeconds(string? duration) → int` (`XmlConvert.ToTimeSpan` for `PT#M#S`; return 0 on null/malformed).
- **PATTERN**: `SkipWatch.Core\Services\Utilities\SrtConverter.cs` (6–46, `ConvertSrtToPrdFormat` only); `SkipWatch.Core\Services\YouTube\DurationParser.cs` (3–21).
- **IMPORTS**: `System.Text`, `System.Text.RegularExpressions` (converter); `System.Xml` (duration); namespace `VideoCortex.Core.Services.Transcripts`.
- **GOTCHA**: Do not port `ConvertSrtToSimpleFormat` (unused). SkipWatch's `mm:ss` is intentionally minute-based (no hours field) — a 75-minute video renders `[75:xx]`; keep that behavior for byte-parity with the reference format the summarizer expects. Apify's `duration` may already be plain seconds or ISO-8601 depending on the actor version — `ParseToSeconds` returns 0 for anything not starting with `PT`; if the captured payload shows a non-`PT` duration, extend the parser to also accept a bare integer/`hh:mm:ss` and document it in NOTES.
- **VALIDATE**: `cd /c/Repos/VideoCortex && dotnet build VideoCortex.Core` exits 0.

#### Task 3: ApifyTranscriptSource

Implement the one-POST Apify client.

- **IMPLEMENT**: `sealed class ApifyTranscriptSource : ITranscriptSource`. Constructor `(HttpClient http, IOptions<ApifySettings> settings, ILogger<ApifyTranscriptSource> logger)`; set `http.Timeout = TimeSpan.FromSeconds(settings.RunTimeoutSeconds)`. `FetchAsync`: guard empty `videoId` and empty `Token` → `Failure(...)`; POST to `https://api.apify.com/v2/acts/streamers~youtube-scraper/run-sync-get-dataset-items?token={escaped}` with body `{ startUrls:[{url:"https://www.youtube.com/watch?v={id}"}], downloadSubtitles:true, preferAutoGeneratedSubtitles:{PreferAutoGenerated}, subtitlesLanguage:{PreferredLanguage} }`; on non-2xx read body → `Failure($"Apify {status}: {body}")`; deserialize `List<ApifyScraperItem>`, take first, `SelectBestSubtitle` (manual-en → any-en → any-with-srt), `SrtConverter.ConvertSrtToPrdFormat` the chosen `srt`; build a success `Transcript` mapping `title→Title`, `channelName→ChannelTitle`, `text→Description`, `DurationParser.ParseToSeconds(duration)→DurationSeconds`, `viewCount/likes/commentsCount`, `thumbnailUrl`, `HasTranscript = !string.IsNullOrWhiteSpace(transcriptText)`. Catch `TaskCanceledException` (when not caller-cancelled) and general `Exception` (not `OperationCanceledException`) → `Failure(...)`. Private DTOs `ApifyRequestBody`, `ApifyStartUrl`, `ApifyScraperItem`, `ApifySubtitle` with `[JsonPropertyName]`.
- **PATTERN**: `SkipWatch.Core\Services\Transcripts\ApifyTranscriptSource.cs` (18–170) — mirror closely.
- **IMPORTS**: `System.Text`, `System.Text.Json`, `System.Text.Json.Serialization`, `Microsoft.Extensions.Logging`, `Microsoft.Extensions.Options`; `VideoCortex.Core.Services.Config` (for `ApifySettings`). No `SkipWatch.*` imports.
- **GOTCHA**: SkipWatch's `ApifyScraperItem` has no `title`/`channelName` fields (it gets those from YouTube). Video Cortex **must** add `[JsonPropertyName("title")]` and a channel field (`[JsonPropertyName("channelName")]` — verify the actual key against the captured payload; the youtube-scraper actor commonly uses `channelName` and `channelId`) so `Title`/`ChannelTitle` populate. Keep the missing-token branch a `Failure` (not a throw) so the runner parks cleanly — this is the documented GOTCHA (no token ⇒ surface error + park, never crash).
- **VALIDATE**: `cd /c/Repos/VideoCortex && dotnet build VideoCortex.Core` exits 0.

#### Task 4: ApifyTranscriptSource + SrtConverter tests (offline)

Prove the source and converter work against a captured payload with no network.

- **IMPLEMENT**: `VideoCortex.Tests/TestData/apify-scraper-item.json` — a minimal one-item array with `title`, `channelName`, `text`, `duration`, `viewCount`, `likes`, `commentsCount`, `thumbnailUrl`, and a `subtitles` array containing one `{ language:"en", type:"manual"|"auto_generated", srt:"1\n00:00:00,000 --> 00:00:02,000\nhello\n\n2\n00:00:02,000 --> 00:00:04,000\nworld\n" }`. A `FakeHttpMessageHandler : HttpMessageHandler` returning a configurable `HttpResponseMessage` (status + JSON body) and capturing the request URL/body for assertions. `ApifyTranscriptSourceTests`: (a) 200 + captured JSON → `Success`, `HasTranscript`, `TranscriptText` contains `[00:00] hello` and `[00:00] world`, `Title`/`ChannelTitle`/`DurationSeconds`/`ViewCount` populated; (b) 200 with a subtitles-less item → `Success` but `!HasTranscript` and metadata still set; (c) empty `Token` in `ApifySettings` → `!Success`, `ErrorMessage` mentions the token, **and the handler was never invoked**; (d) non-2xx (e.g. 429 body "rate limited") → `!Success`, error carries status+body; (e) empty array `[]` → `!Success`. `SrtConverterTests`: multi-cue SRT → expected `[mm:ss] text` lines; an entry with an hours field (e.g. `01:05:03,000`) → `[65:03]`; malformed/blank input → empty string. `DurationParser`: `"PT4M13S"→253`, null/`""`/`"garbage"`→0.
- **PATTERN**: SkipWatch has no `ApifyTranscriptSource` test to copy directly — model the handler on the standard "fake `HttpMessageHandler`" pattern; model the runner-style fake/queue on `SkipWatch.Tests\...\TranscriptIngestRunnerTests.cs` (257–272).
- **IMPORTS**: `System.Net`, `System.Net.Http`, `System.Text`, `Microsoft.Extensions.Logging.Abstractions`, `Microsoft.Extensions.Options`, `FluentAssertions`, `Xunit`, `VideoCortex.Core.Services.Transcripts`, `VideoCortex.Core.Services.Config`.
- **GOTCHA**: Load the JSON via a copied-to-output test-data file (`CopyToOutputDirectory=PreserveNewest` in the test `.csproj`) or an embedded resource — do not hardcode a machine path. `HttpClient` needs a `BaseAddress`-free absolute URL (the source builds the absolute Apify URL itself). The token-missing test asserts the handler count stayed 0 to prove no wasted/paid call.
- **VALIDATE**: `cd /c/Repos/VideoCortex && dotnet test VideoCortex.slnx --filter "FullyQualifiedName~Transcripts.SrtConverterTests|FullyQualifiedName~Transcripts.ApifyTranscriptSourceTests"` exits 0.

### Milestone 2: Ingest runner

The per-video runner applies a `Transcript` to a `Video` row and drives status/backoff/park.

**Validation checkpoint**: the runner tests (Task 6) cover success-with/without-transcript, retry+backoff, and park, all on in-memory SQLite with a fake source.

#### Task 5: TranscriptIngestRunner + interface

Implement the per-video logic in `.Core`.

- **IMPLEMENT**: `ITranscriptIngestRunner` with `Task<TranscriptIngestResult> RunAsync(Video video, CancellationToken ct = default)`; enum `TranscriptIngestOutcome { Transcribed, NoTranscript, Retry, Parked }`; record `TranscriptIngestResult(TranscriptIngestOutcome Outcome, int RetryCount, string? Error, int ElapsedMs)`. `sealed class TranscriptIngestRunner(VideoCortexDbContext db, ITranscriptSource transcripts, IOptions<TranscriptWorkerSettings> settings, ILogger<TranscriptIngestRunner> logger)`. Constants `BaseBackoff = 60s`, `MaxBackoff = 1h`. In `RunAsync`: `Stopwatch`; call `transcripts.FetchAsync` inside `try/catch` (rethrow only caller-`OperationCanceledException`; any other throw → failed `Transcript(false, …, ex.Message)`). Failure path: `RetryCount++`, `LastError`; park if `RetryCount >= settings.MaxRetryAttempts` (`Parked=true`, `NextAttemptAt=null`) else `NextAttemptAt = UtcNow + min(1h, 60s × 2^(RetryCount-1))`; `Status` stays `Added`; `SaveChangesAsync`; return `Retry`/`Parked`. Success path: backfill `Title`/`ChannelTitle`/`Description`/`DurationSeconds`/`ViewCount`/`LikeCount`/`CommentsCount`/`ThumbnailUrl` **only when non-null**; if `HasTranscript` set `TranscriptText`/`TranscriptLang`/`TranscribedAt`/`Status=Transcribed` else `Status=NoTranscript`; reset `RetryCount=0`/`LastError=null`/`NextAttemptAt=null`; `SaveChangesAsync`; return `Transcribed`/`NoTranscript`. Log one info line per outcome.
- **PATTERN**: `SkipWatch.Core\Services\Transcripts\TranscriptIngestRunner.cs` (13–172) and `ITranscriptIngestRunner.cs` (5–26) — mirror, **minus** `ActivityEntry`, `JobEventBus`/`PublishSafe`, and `DiscoverySettings` (use `TranscriptWorkerSettings.MaxRetryAttempts`).
- **IMPORTS**: `System.Diagnostics`, `Microsoft.EntityFrameworkCore`, `Microsoft.Extensions.Logging`, `Microsoft.Extensions.Options`, `VideoCortex.Core.Db`, `VideoCortex.Core.Entities`, `VideoCortex.Core.Services.Config`.
- **GOTCHA**: Backoff uses `RetryCount` *after* the increment: first failure `RetryCount=1` → `60s × 2^0 = 60s`. Compute via `TimeSpan.FromTicks(Math.Min(MaxBackoff.Ticks, BaseBackoff.Ticks * (long)Math.Pow(2, RetryCount-1)))` to avoid overflow at high retry counts. Do **not** reference any `ParkedAt`/`HasTranscript` column unless Phase 1 actually added it — the PRD data model (§9) has neither; use `Parked` and rely on `Status`/`TranscriptText` for "has transcript".
- **VALIDATE**: `cd /c/Repos/VideoCortex && dotnet build VideoCortex.Core` exits 0.

#### Task 6: TranscriptIngestRunner tests

Cover the runner's branches against in-memory SQLite with a fake source.

- **IMPLEMENT**: `TranscriptIngestRunnerTests` — keep-alive `SqliteConnection("Data Source=:memory:")`, `DbContextOptionsBuilder<VideoCortexDbContext>().UseSqlite(conn)`, `db.Database.Migrate()`. Seed a `Project` + a `Video(Status=Added, DurationSeconds=600, ViewCount=100, …)`. `FakeTranscriptSource : ITranscriptSource` with `Queue<Transcript> Responses` + `Exception? ThrowOnNext`. Tests: (a) success-with-transcript → `Outcome=Transcribed`, row `Status=Transcribed`, `TranscriptText`/`TranscriptLang`/`TranscribedAt` set, metadata backfilled, `RetryCount=0`, `LastError=null`; (b) success-no-transcript → `Outcome=NoTranscript`, `Status=NoTranscript`, `TranscriptText=null`, metadata still set; (c) cheap-fields-not-overwritten-when-Apify-null → `ViewCount` stays `100`, `DurationSeconds` stays `600`; (d) first failure → `Outcome=Retry`, `Status=Added`, `RetryCount=1`, `Parked=false`, `NextAttemptAt≈+60s`, `LastError` set; (e) at `MaxRetryAttempts` (seed `RetryCount=maxRetry-1`) → `Outcome=Parked`, `Parked=true`, `NextAttemptAt=null`; (f) backoff doubles then caps at 3600s across increasing `RetryCount`; (g) `ThrowOnNext` → treated as `Retry`, `LastError="boom"`.
- **PATTERN**: `SkipWatch.Tests\Services\Transcripts\TranscriptIngestRunnerTests.cs` (14–273) — mirror scenarios; adapt seeding to `Project`→`Video` (no `Channel`) and drop `JobEventBus`/`Activity` assertions.
- **IMPORTS**: `Microsoft.Data.Sqlite`, `Microsoft.EntityFrameworkCore`, `Microsoft.Extensions.Logging.Abstractions`, `Microsoft.Extensions.Options`, `FluentAssertions`, `Xunit`, `VideoCortex.Core.Db`, `VideoCortex.Core.Entities`, `VideoCortex.Core.Services.Transcripts`.
- **GOTCHA**: `:memory:` DB vanishes when the connection closes — keep one open connection per test (dispose `conn` + `db` with `using`). Use `Options.Create(new TranscriptWorkerSettings { MaxRetryAttempts = 3 })`. Timing assertions use `BeApproximately(60, 5)` etc. — never assert exact ticks.
- **VALIDATE**: `cd /c/Repos/VideoCortex && dotnet test VideoCortex.slnx --filter "FullyQualifiedName~Transcripts.TranscriptIngestRunnerTests"` exits 0.

### Milestone 3: Worker + DI wiring

The hosted worker polls the queue, hands rows to the runner, and everything is registered.

**Validation checkpoint**: the full suite is green and the app boots with the worker registered (no unresolved-service exception at startup).

#### Task 7: TranscriptWorker + DI registration

Add the polling `BackgroundService` and wire the whole stage into `Program.cs`.

- **IMPLEMENT**: `sealed class TranscriptWorker : BackgroundService` in `VideoCortex\Workers\` (namespace `VideoCortex.Workers`), ctor `(IServiceScopeFactory scopeFactory, IOptionsMonitor<TranscriptWorkerSettings> monitor, ILogger<TranscriptWorker> logger)`. `ExecuteAsync`: loop while not cancelled — `didWork = await TickOnceAsync(ct)` (catch `OperationCanceledException` → break; catch other → log + `didWork=false`); when `!didWork` `await Task.Delay(IdlePollSeconds, ct)` (catch cancel → break). `TickOnceAsync`: open a scope, resolve `VideoCortexDbContext` + `ITranscriptIngestRunner`; query the first `Video` where `Status == VideoStatus.Added && !Parked && (NextAttemptAt == null || NextAttemptAt <= now)`, ordered by `NextAttemptAt` then `AddedAt`; if none return `false`; else `await runner.RunAsync(video, ct)`, return `true`. In `Program.cs` add: `Configure<ApifySettings>(GetSection("Apify"))` and `Configure<TranscriptWorkerSettings>(GetSection("TranscriptWorker"))` if Phase 1 did not already; `AddHttpClient<ITranscriptSource, ApifyTranscriptSource>()`; `AddScoped<ITranscriptIngestRunner, TranscriptIngestRunner>()`; `AddHostedService<TranscriptWorker>()`.
- **PATTERN**: `SkipWatch\Services\Workers\TranscriptWorker.cs` (9–81); `SkipWatch\Program.cs` (45, 54–56, 73–74, 80). Adapt `Discovered→Added`, `IngestedAt→AddedAt`; drop the `Concurrency` field if `TranscriptWorkerSettings` lacks it.
- **IMPORTS** (worker): `Microsoft.EntityFrameworkCore`, `Microsoft.Extensions.Options`, `VideoCortex.Core.Db`, `VideoCortex.Core.Entities`, `VideoCortex.Core.Services.Config`, `VideoCortex.Core.Services.Transcripts`. (`Microsoft.Extensions.Hosting`/`DependencyInjection`/`Logging` come via the web SDK's global usings — add explicit usings if the build flags them.)
- **GOTCHA**: The worker is a **singleton** (`AddHostedService`); it must never inject the scoped `DbContext`/runner directly — resolve them inside the per-tick scope. Register the runner as **scoped** (it holds the scoped `DbContext`). The typed-client registration (`AddHttpClient<ITranscriptSource, ApifyTranscriptSource>`) both provides the `HttpClient` and registers the source — do not also `AddScoped<ITranscriptSource>`. Confirm `Configure<ApifySettings>` is present exactly once (Phase 1 may already do it).
- **VALIDATE**: `cd /c/Repos/VideoCortex && dotnet build VideoCortex.slnx` exits 0.

### Milestone 4: Testing & Validation

Whole-suite green + a boot smoke proving the worker resolves at startup.

**Validation checkpoint**: `dotnet test VideoCortex.slnx` passes; the app boots with the worker registered and does not throw.

#### Task 8: Full suite + boot smoke

Run the complete regression and confirm the host starts with the new stage wired.

- **IMPLEMENT**: No new production code. Ensure the test `.csproj` copies `TestData/*.json` to output. Run the full suite. Then a non-interactive boot smoke: start the app with a **blank** `Apify:Token` and no pasted videos (nothing for the worker to do), confirm it serves `/` (200) and stays up (the worker idles without throwing), then stop it. This proves DI resolves the worker → runner → source graph and that an unconfigured token does not crash startup.
- **PATTERN**: Phase 1's boot-smoke VALIDATE (curl-until-200 then kill).
- **IMPORTS**: n/a.
- **GOTCHA**: The boot smoke must not depend on Apify — with a blank token and an empty `Added` queue the worker simply idles; do **not** seed a video that would trigger a (paid, network) Apify call in an automated check. A real end-to-end transcript run is a separate, **manual, optional** step: set a real `Apify:Token`, paste a known captioned video, and watch the row reach `Transcribed` (document this in NOTES; it is not a CI gate).
- **VALIDATE**: `cd /c/Repos/VideoCortex && dotnet test VideoCortex.slnx` exits 0; then `cd /c/Repos/VideoCortex && (dotnet run --project VideoCortex --urls http://localhost:5399 & APP=$!; for i in $(seq 1 40); do curl -fsS http://localhost:5399/ >/dev/null && break; sleep 1; done; curl -fsS http://localhost:5399/ >/dev/null; RC=$?; kill $APP 2>/dev/null; exit $RC)` exits 0.

### Milestone 5: Commit, push, and open PR (mandatory)

**Validation checkpoint**: Branch `phase-4-transcript` pushed to origin; PR open against `main`.

#### Task 9: Commit, push, and open PR

- **IMPLEMENT**:
  - Create/switch to branch `phase-4-transcript` off `main`.
  - Stage and commit all changes: message `Phase 4: Transcript — Apify transcript+metadata source, ingest runner, polling worker`.
  - Push: `git push -u origin phase-4-transcript`.
  - Open PR: `gh pr create --base main --head phase-4-transcript --title "Phase 4: Transcript" --body "<body>"` where `<body>` is the ACCEPTANCE CRITERIA as a checked checklist + a `## Notes` section.
- **GOTCHA**: **Same precondition as prior phases** — an `origin` remote must exist (`git remote add origin <url>`) and `gh auth status` must succeed. If neither is set up, stop after the local commit and report that push/PR require a remote — do not fail silently. Do not add Claude as a commit author or `Co-Authored-By` (repo convention: commits authored solely by the user).
- **VALIDATE**: `gh pr view --json number,title,state,headRefName` returns the PR with `"state":"OPEN"` and `"headRefName":"phase-4-transcript"` (only runnable once a remote exists).

---

## TESTING STRATEGY

### Unit Tests
- `SrtConverter.ConvertSrtToPrdFormat`: multi-cue → `[mm:ss] text`; hours folded into minutes (`01:05:03` → `[65:03]`); malformed/blank → empty.
- `DurationParser.ParseToSeconds`: `PT#M#S` → seconds; null/empty/garbage → 0.
- `ApifyTranscriptSource` (mocked `HttpMessageHandler`, no network): 200+payload → success with transcript + metadata; 200 no-subtitles → success + `!HasTranscript`; blank token → failure and handler never called; non-2xx → failure with status+body; empty dataset → failure.

### Integration Tests
- `TranscriptIngestRunner` on in-memory SQLite + fake `ITranscriptSource`: status transitions (`Transcribed`, `NoTranscript`), retry + `~60s` backoff, doubling/cap, park at max retries, thrown-exception-as-transient, and "don't overwrite good metadata with null".
- Boot smoke (Task 8): host starts with the worker registered, serves `/`, idles cleanly with a blank Apify token.

### Edge Cases
- Missing `Apify:Token` → clean `Failure` → runner parks after retries; never an uncaught throw.
- Video genuinely without captions → `NoTranscript`, metadata retained, no further pipeline work.
- Apify returns partial metadata (some nulls) → existing non-null `Video` columns preserved.
- Backoff overflow guard at high `RetryCount` (no negative/overflowed `NextAttemptAt`).
- Worker queue empty → `TickOnceAsync` returns `false` → idle sleep (no busy-spin).

---

## VALIDATION COMMANDS

Execute every command to ensure zero regressions and 100% phase correctness. **None of these touch the network / Apify.**

### Level 1: Syntax & Style
```bash
cd /c/Repos/VideoCortex && dotnet build VideoCortex.slnx -warnaserror
```
**Expected**: exit 0 (warnings-as-errors enforced via `Directory.Build.props`).

### Level 2: Unit Tests
```bash
cd /c/Repos/VideoCortex && dotnet test VideoCortex.slnx --filter "FullyQualifiedName~Transcripts"
```
**Expected**: exit 0; SRT/duration, Apify source (mocked), and ingest-runner tests all green.

### Level 3: Integration Tests
Covered by the same `dotnet test VideoCortex.slnx` run (runner on in-memory SQLite):
```bash
cd /c/Repos/VideoCortex && dotnet test VideoCortex.slnx
```

### Level 4: Manual Validation (boot smoke — offline)
```bash
cd /c/Repos/VideoCortex && (dotnet run --project VideoCortex --urls http://localhost:5399 & APP=$!; for i in $(seq 1 40); do curl -fsS http://localhost:5399/ >/dev/null && break; sleep 1; done; curl -fsS http://localhost:5399/ >/dev/null; RC=$?; kill $APP 2>/dev/null; exit $RC)
```
**Expected**: exit 0; app serves `/` with the transcript worker registered and idling (blank token, empty queue → no Apify call).

### Optional (manual, NOT a CI gate — costs money)
With a real `Apify:Token` set and a known captioned video pasted into a project, confirm the row advances `Added → Transcribed` with title/channel/duration filled in, and that a caption-less video lands in `NoTranscript`. Never run this in automation.

---

## ACCEPTANCE CRITERIA

- [ ] `ITranscriptSource` + `Transcript` record exist in `.Core`; `Transcript` carries transcript text/lang + `Title`/`ChannelTitle` + description/duration/counts/thumbnail + `Success`/`HasTranscript`/`ErrorMessage`.
- [ ] `ApifyTranscriptSource` makes one `run-sync-get-dataset-items` POST via an `IHttpClientFactory`-provided `HttpClient`, converts SRT → `[mm:ss] text`, and fails **cleanly** (no uncaught throw) on missing token / non-2xx / timeout / empty dataset.
- [ ] `SrtConverter` and `DurationParser` are unit-tested directly; `ApifyTranscriptSource` is tested against a captured payload via a mocked `HttpMessageHandler` with **no network call**.
- [ ] `TranscriptIngestRunner` transitions `Added → Transcribed` (with transcript) / `Added → NoTranscript` (no subtitles, metadata retained), applies `60s × 2^(n-1)` backoff capped at 1h, and parks after `MaxRetryAttempts`; verified on in-memory SQLite with a fake source.
- [ ] Metadata backfill overwrites `Video` columns only when Apify returned a non-null value.
- [ ] `TranscriptWorker` polls `Status==Added && !Parked && due`, delegates to the runner in a per-tick DI scope, and idles when the queue is empty; registered as a hosted service with the source (typed client) + runner (scoped) wired in `Program.cs`.
- [ ] There is **no YouTube Data API** dependency; Apify is the sole metadata source.
- [ ] All validation commands pass; the boot smoke succeeds with a blank Apify token; no test touches the network.

---

## COMPLETION CHECKLIST

- [ ] All tasks completed in order
- [ ] Each task validation passed immediately
- [ ] Level 1: `dotnet build -warnaserror` clean
- [ ] Level 2/3: `dotnet test` green (SRT, duration, Apify source, ingest runner)
- [ ] Level 4: boot smoke exits 0 with a blank token (worker idles, no Apify call)
- [ ] All acceptance criteria met
- [ ] Branch `phase-4-transcript` pushed and PR opened against `main` (final task; requires remote)

---

## NOTES

- **Base branch is `main`**, not `master`. Branch: `phase-4-transcript` off `main`. PR-based workflow — never commit directly to `main`.
- **No SkipWatch dependency**: SkipWatch is a read-only pattern reference. Do not add a project reference; do not copy its `ActivityEntry` logging, `JobEventBus`, `DiscoverySettings`, or `Channel` model — none exist in Video Cortex. The runner uses `TranscriptWorkerSettings.MaxRetryAttempts` where SkipWatch used `DiscoverySettings.MaxRetryAttempts`.
- **Record field order is a contract**: `Transcript` is constructed positionally by the runner and every test. Fix the order in Task 1 and keep it.
- **Column assumptions**: PRD §9 defines `Parked` (bool) but **no** `ParkedAt` and **no** `HasTranscript` column. Do not reference those unless Phase 1 actually added them; derive "has transcript" from `Status == Transcribed` / non-null `TranscriptText`. If Phase 1's `Video` diverges from these assumptions, adapt to the real columns and note the deviation in the PR.
- **`Title`/`ChannelTitle` come from Apify only** (no YouTube Data API). This is the one deliberate divergence from SkipWatch's `Transcript`/`ApifyScraperItem`, which omit them. Verify the actual Apify JSON keys for channel (`channelName` / `channelId`) against the captured `TestData/apify-scraper-item.json`; if the youtube-scraper actor version in use names them differently, map to the real keys and record it here.
- **Apify duration format**: `DurationParser.ParseToSeconds` expects ISO-8601 (`PT…`). If the captured payload shows a bare-seconds or `hh:mm:ss` duration instead, extend the parser (accept a plain integer / `hh:mm:ss`) and document the chosen behavior; keep `0` as the "unknown" sentinel so the runner leaves an existing `DurationSeconds` untouched.
- **Cost / network discipline**: real Apify calls cost money. Tests and every VALIDATE step are fully offline (fakes/mocks). The only real call is an explicit, optional, manual end-to-end check with a real token — never wired into CI.
- **Remote/PR precondition**: no `origin` remote may exist at planning time. The final task documents that push + PR require adding a remote and authenticating `gh`; absent that, the phase ends at a local commit.
- **Commit authorship**: commits are authored solely by the user — do not add Claude as author or `Co-Authored-By`.
