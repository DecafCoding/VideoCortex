# Phase 5: Summary → Concept Page

The following plan should be complete, but it is important that you validate documentation and codebase patterns and task sanity before you start implementing.

Pay special attention to naming of existing utils, types, and models. Import from the right files.

## Phase Description

This phase adds the **per-video summarization stage** to Video Cortex. For every `Video` sitting at `Status = Transcribed`, a polling background worker calls an OpenAI-compatible LLM to produce a structured summary (`title`, `description`, `tags`, `body_markdown`), renders the Markdown body to HTML with **Markdig**, fills the bundled OKF **concept template**, and writes a per-video OKF concept page to disk inside the project's library folder (`<Project folder>/<concept-slug>.html`). The video's summary fields and `ConceptSlug` are persisted, `SummarizedAt` is stamped, and the video advances to `Status = Summarized` so Phase 6's report worker can pick it up.

This introduces the LLM layer the rest of the app builds on: a thin `OpenAiClient` over any OpenAI-compatible `/v1/chat/completions` endpoint, backed by a per-endpoint `HttpClient` cache (`OpenAiClientCache`) that honors `LlmSettings` (`Model` / `BaseUrl` / `ApiKey` / timeout) from Phase 1 and rebuilds on config change via `IOptionsMonitor` — the same cache Phase 6 reuses for report synthesis and Phase 7 relies on for hot reload. It also extends `OkfLibraryStore` (created in Phase 2) with a `WriteConceptPageAsync` method so concept-page writing goes through the one deterministic, template-driven writer.

The reference implementation for every pattern here is **SkipWatch** at `C:\Repos\SkipWatch\SkipWatch` (a separate repo). Read those files to copy conventions, then write clean, minimal Video Cortex equivalents — do **not** add a project reference to SkipWatch and do **not** copy its wiki/guide/quota/decision-signal code. The key SkipWatch difference: SkipWatch summarizes only the first few minutes of a transcript for a triage card; Video Cortex needs a reasonably **complete** per-video summary because Phase 6's report is synthesized from these summaries, so the transcript window is much wider (see NOTES).

## User Stories

As the single local user
I want each transcribed video summarized into its own readable page
So that I can grasp a video's substance without watching it, and the page lives in my on-disk OKF library alongside my hand-built ones.

As the operator
I want the summarizer to talk to any OpenAI-compatible endpoint (hosted OpenAI or a local Ollama `/v1`) and to back off and park on repeated failure
So that a bad video or a down endpoint never spins the CPU, spams the provider, or blocks the pipeline.

## Problem Statement

After Phase 4, videos reach `Status = Transcribed` with transcript text and Apify metadata, but nothing consumes them. There is no LLM client, no summarizer, no concept-page writer, and no worker to advance transcribed videos. Without this stage the pipeline dead-ends at `Transcribed`, no concept pages are ever written, and Phase 6 has no per-video summaries to synthesize a report from.

## Solution Statement

Add `VideoCortex.Core/Services/Llm/` with `OpenAiClientCache` (per-endpoint `HttpClient` cache keyed by `BaseUrl|ApiKey|timeout`, rebuilt on change) and `OpenAiClient` (a thin `/v1/chat/completions` wrapper), plus `IVideoSummarizer` / `OpenAiSummarizer` issuing a single `response_format = json_schema` chat call that returns `{ title, description, tags[], body_markdown }` and threads the project's `AIInstructions` into the prompt. Extend `OkfLibraryStore` with `WriteConceptPageAsync(project, video, summary)` that renders `body_markdown` → HTML via Markdig, fills the OKF concept template deterministically (never letting the LLM emit page HTML), and atomically writes `<Project>/<concept-slug>.html`. Wire a per-video `SummaryIngestRunner` (in `.Core`, same backoff/park shape as Phase 4's transcript runner) driven by a hosted polling `SummaryWorker`. On success the runner persists the summary fields + `ConceptSlug` + `SummarizedAt` and sets `Status = Summarized`. Cover the summarizer with a mocked `HttpMessageHandler` (canned json_schema completion, no network) and the writer with a temp-dir round-trip that asserts valid, conformant OKF output.

## Phase Metadata

**Phase Type**: New Capability (pipeline stage + LLM layer)
**Estimated Complexity**: Medium-High
**Primary Systems Affected**: New LLM service layer (`Services/Llm/`), `OkfLibraryStore` (extended), new `SummaryIngestRunner` + `SummaryWorker`, `Video` persistence, DI composition, config (`Summary` section binding `SummarySettings` + `LlmSettings`)
**Dependencies**: Phases 1–4. Specifically: `LlmSettings` / `SummarySettings` records + config overlay (Phase 1); `OkfLibraryStore`, the bundled OKF concept template in `wwwroot/okf/concept.html`, and slug helpers (Phase 2); `Video` entity with `TranscriptText` / summary columns / status enum (Phase 1) populated to `Transcribed` by the transcript worker (Phase 4). New package: **Markdig** (add to `VideoCortex.Core`).

---

## CONTEXT REFERENCES

### Relevant Codebase Files IMPORTANT: YOU MUST READ THESE FILES BEFORE IMPLEMENTING!

These are **SkipWatch reference files** (a separate repo) — read them for patterns to mirror, then write minimal Video Cortex equivalents. Do not reference or depend on the SkipWatch projects.

- `C:\Repos\SkipWatch\SkipWatch\SkipWatch.Core\Services\OpenAi\OpenAiClientCache.cs` — Why: the per-endpoint `HttpClient` cache. Keyed on `effectiveBase|apiKey|timeoutSeconds`; null/blank base URL → `DefaultBaseUrl = "https://api.openai.com"`; throws on blank API key; rebuilds under a lock only when the key differs; abandons (does not dispose) the old client to avoid cancelling in-flight calls. **Copy this shape almost verbatim** — it is the hot-reload substrate Phase 7 depends on.
- `C:\Repos\SkipWatch\SkipWatch\SkipWatch.Core\Services\Summarization\OpenAiSummarizer.cs` — Why: the OpenAI-compatible chat call. Mirror the `ChatCompletionRequest` / `ChatMessage` / `ResponseFormat` / `JsonSchemaSpec` / `ChatCompletionResponse` private DTOs, the `response_format = json_schema` (`strict = true`) construction, `PostAsJsonAsync("/v1/chat/completions", …)`, non-2xx → read error body → throw `HttpRequestException`, and `JsonSerializer.Deserialize` of the choice content with a parse-exception guard. **Our output schema differs** (4 fields, not 1) and our transcript window is wider.
- `C:\Repos\SkipWatch\SkipWatch\SkipWatch.Core\Services\Summarization\ISummarizer.cs` — Why: the single-call interface shape (`SummarizeAsync(title, channel, slice, ct)`). Ours adds an `aiInstructions` parameter.
- `C:\Repos\SkipWatch\SkipWatch\SkipWatch.Core\Services\Summarization\SummaryResponse.cs` — Why: the `record` + `[property: JsonPropertyName(...)]` mapping style for the structured-output target. Ours carries `title` / `description` / `tags` / `body_markdown`.
- `C:\Repos\SkipWatch\SkipWatch\SkipWatch.Core\Services\Summarization\SummaryParseException.cs` — Why: the dedicated exception the runner treats as a transient failure. Mirror verbatim (drop the `decision_signal` mention in the doc-comment).
- `C:\Repos\SkipWatch\SkipWatch\SkipWatch.Core\Services\Summarization\TranscriptSlicer.cs` — Why: the `[mm:ss]`-prefix time-window slicer. We reuse the *technique* but with a much larger window (or none — see NOTES); document the choice.
- `C:\Repos\SkipWatch\SkipWatch\SkipWatch.Core\Services\Summarization\SummaryIngestRunner.cs` — Why: the per-video runner: guard the precondition (empty transcript → `InvalidOperationException`), `Stopwatch` timing, call the summarizer, on success write fields + advance status + reset retry state + save, on failure increment `RetryCount`, set `LastError`, and either park (at `MaxRetryAttempts`) or set `NextAttemptAt` via exponential backoff `BaseBackoff(60s) × 2^(n-1)` capped at `MaxBackoff(1h)`. **Mirror this control flow exactly.** Drop SkipWatch's `Activity`/`ActivityEntry` logging and `JobEventBus` publish (Video Cortex has neither).
- `C:\Repos\SkipWatch\SkipWatch\SkipWatch\Services\Workers\SummaryWorker.cs` — Why: the hosted polling `BackgroundService`: `ExecuteAsync` loop, `TickOnceAsync` opens a DI scope, queries the oldest eligible row (`Status == Transcribed && !Parked && (NextAttemptAt == null || NextAttemptAt <= now)`, ordered by `NextAttemptAt` then `TranscribedAt`), runs the runner, returns whether it did work; sleep `IdlePollSeconds` when idle. Mirror; use `IOptionsMonitor<SummarySettings>` for the idle interval.
- `C:\Repos\SkipWatch\SkipWatch\SkipWatch.Core\Services\Summarization\SummaryWorkerSettings.cs` — Why: the config record's `Model` / `BaseUrl` / `ApiKey` / `RequestTimeoutSeconds` / `IdlePollSeconds` doc-comments explaining OpenAI-compatible endpoints (llama.cpp, LM Studio, vLLM, Ollama `/v1`). Video Cortex splits these across `LlmSettings` (endpoint) + `SummarySettings` (worker knobs) per Phase 1.

### Video Cortex files created in earlier phases (READ, then extend/consume — do not recreate)

- `C:\Repos\VideoCortex\VideoCortex.Core\Services\Library\OkfLibraryStore.cs` — Why: extend with `WriteConceptPageAsync`. Reuse its template-loading, placeholder-fill, atomic-write, and slug helpers from Phase 2 (`CreateLibrary` already loads `concept.html`/`index.html` and writes atomically). Confirm the exact method/helper names before adding to it.
- `C:\Repos\VideoCortex\VideoCortex.Core\Services\Config\Settings.cs` — Why: consume `LlmSettings` (Model/BaseUrl/ApiKey/RequestTimeoutSeconds) and `SummarySettings` (IdlePollSeconds/MaxRetryAttempts). Confirm the field names Phase 1 shipped; if `LlmSettings` lacks a timeout field or `SummarySettings` lacks `MaxRetryAttempts`, add them (init-property record shape, defaults) rather than inventing a parallel record.
- `C:\Repos\VideoCortex\VideoCortex.Core\Entities\Video.cs` — Why: persist `SummaryTitle` / `SummaryDescription` / `SummaryBodyMd` / `ConceptSlug` / `SummarizedAt` and set `Status = VideoStatus.Summarized`; read `TranscriptText`, `Title`, `ChannelTitle`, `YoutubeVideoId`, `ProjectId`. All columns already exist (Phase 1 migration) — **no new migration this phase.**
- `C:\Repos\VideoCortex\VideoCortex.Core\Entities\Enums.cs` — Why: `VideoStatus.Transcribed` (worker input) and `VideoStatus.Summarized` (worker output); do not reorder.
- `C:\Repos\VideoCortex\VideoCortex\wwwroot\okf\concept.html` — Why: the concept template to fill. Placeholders: `{{TITLE}}` (tab + `<h1>`), `{{THEME_HREF}}`, `{{TYPE}}`, `{{DESCRIPTION}}`, `{{TAGS}}` (JSON array *contents* inside `[…]`), `{{RESOURCE}}` (a raw JSON value — quoted string or `null`), `{{TIMESTAMP}}`, `{{TAG_CHIPS}}`, `{{BODY}}`. **Do not** re-wrap in `[…]` or quotes where the template already supplies them (see the GOTCHA).
- `C:\Repos\VideoCortex\VideoCortex\Program.cs` — Why: register `OpenAiClientCache` / `OpenAiClient` / `IVideoSummarizer` and the hosted `SummaryWorker`; confirm `OkfLibraryStore` is already registered from Phase 2.

### Relevant Documentation YOU SHOULD READ THESE BEFORE IMPLEMENTING!

- [OpenAI Chat Completions — Structured Outputs (`response_format: json_schema`)](https://platform.openai.com/docs/guides/structured-outputs) — Why: strict mode requires `additionalProperties: false` and every property listed in `required`; the schema must mirror the response record exactly.
- [Markdig on NuGet / GitHub](https://github.com/xoofx/markdig) — Why: `Markdown.ToHtml(markdown, pipeline)`; use `new MarkdownPipelineBuilder().UseAdvancedExtensions().Build()` (tables, fenced code, etc.). Add the package to `VideoCortex.Core`.
- [Options pattern + `IOptionsMonitor`](https://learn.microsoft.com/en-us/dotnet/core/extensions/options) — Why: read `CurrentValue` per call so a Settings edit (overlay reload) changes the endpoint without a restart.
- [`IHttpClientFactory` vs. long-lived clients](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/use-httpclientfactory-to-implement-resilient-http-requests) — Why: the cache holds a small, bounded set of long-lived clients keyed by endpoint; justified over per-request factory clients because base address + auth header are endpoint-specific.
- OKF-HTML spec: `C:\Users\JasonUser\Documents\SecondBrain\okf\SPEC.md` — §4 concept documents (metadata block, required non-empty `type`, head boilerplate, body starts with `<h1>`), §3.2 relative links, §9 conformance. Why: the concept page must conform.
- Reference library `C:\Users\JasonUser\Documents\SecondBrain\Wild Flowers\` — Why: eyeball a real concept page for the look the generated page must match.

### New Files to Create

- `C:\Repos\VideoCortex\VideoCortex.Core\Services\Llm\OpenAiClientCache.cs` — per-endpoint `HttpClient` cache (mirror SkipWatch).
- `C:\Repos\VideoCortex\VideoCortex.Core\Services\Llm\OpenAiClient.cs` — thin wrapper: `PostChatAsync(LlmSettings, request, ct)` over `/v1/chat/completions`, owning one `OpenAiClientCache`; shared by summarizer (this phase) and report synthesizer (Phase 6).
- `C:\Repos\VideoCortex\VideoCortex.Core\Services\Llm\IVideoSummarizer.cs` — `Task<VideoSummary> SummarizeAsync(string title, string channel, string transcript, string? aiInstructions, CancellationToken ct = default)`.
- `C:\Repos\VideoCortex\VideoCortex.Core\Services\Llm\OpenAiSummarizer.cs` — `IVideoSummarizer` impl issuing the json_schema chat call.
- `C:\Repos\VideoCortex\VideoCortex.Core\Services\Llm\VideoSummary.cs` — `record VideoSummary` mapping `{ title, description, tags[], body_markdown }`.
- `C:\Repos\VideoCortex\VideoCortex.Core\Services\Llm\SummaryParseException.cs` — thrown on unparseable/empty structured output.
- `C:\Repos\VideoCortex\VideoCortex.Core\Services\Llm\TranscriptWindow.cs` — char/time-bounded transcript trimmer (technique from SkipWatch `TranscriptSlicer`; wide window — see NOTES).
- `C:\Repos\VideoCortex\VideoCortex.Core\Services\Summary\ISummaryIngestRunner.cs` + `SummaryIngestRunner.cs` — per-video runner (backoff/park like Phase 4).
- `C:\Repos\VideoCortex\VideoCortex.Core\Services\Summary\SummaryIngestResult.cs` — result record/enum (`Summarized | Retry | Parked`), mirroring Phase 4's transcript result type.
- `C:\Repos\VideoCortex\VideoCortex\Workers\SummaryWorker.cs` — hosted polling `BackgroundService`.
- `C:\Repos\VideoCortex\VideoCortex.Tests\Llm\OpenAiSummarizerTests.cs` — mocked `HttpMessageHandler`, canned completion, no network.
- `C:\Repos\VideoCortex\VideoCortex.Tests\Library\ConceptPageWriterTests.cs` — temp-dir round-trip + OKF conformance assertions.
- `C:\Repos\VideoCortex\VideoCortex.Tests\Summary\SummaryIngestRunnerTests.cs` — success/retry/park on in-memory SQLite with a fake summarizer.

### Files to Modify

- `C:\Repos\VideoCortex\VideoCortex.Core\Services\Library\OkfLibraryStore.cs` — add `WriteConceptPageAsync(Project project, Video video, VideoSummary summary, CancellationToken ct = default)` (and, if not already present, a public `ConceptSlug`/collision helper).
- `C:\Repos\VideoCortex\VideoCortex.Core\Services\Library\IOkfLibraryStore.cs` — add the new method to the interface (if Phase 2 introduced one).
- `C:\Repos\VideoCortex\VideoCortex.Core\VideoCortex.Core.csproj` — add the **Markdig** `PackageReference`.
- `C:\Repos\VideoCortex\VideoCortex\Program.cs` — register `OpenAiClient`, `IVideoSummarizer`, `ISummaryIngestRunner` (scoped), and `AddHostedService<SummaryWorker>()`.
- `C:\Repos\VideoCortex\VideoCortex\appsettings.json` — ensure `Summary` / `Llm` sections exist with non-secret defaults (blank `ApiKey`).

### Patterns to Follow

**Naming**: PascalCase types/methods, `I`-prefixed interfaces, `record` for DTOs/results, one type per file, enums in declaration order (persisted as ints — do not reorder).

**LLM client** (mirror SkipWatch `OpenAiClientCache` + `OpenAiSummarizer`): read `IOptionsMonitor<T>.CurrentValue` per call; resolve the client from the cache with `(BaseUrl, ApiKey, timeout)`; blank `BaseUrl` → `https://api.openai.com`; blank `ApiKey` → throw `InvalidOperationException` with a Settings-pointing message; `POST /v1/chat/completions`; non-2xx → read body → `HttpRequestException`; deserialize the choice content → parse guard → `SummaryParseException`.

**Structured output**: `response_format = { type: "json_schema", json_schema: { name, strict: true, schema } }`; the schema is `additionalProperties: false` with **all four** properties in `required`; the schema mirrors `VideoSummary` exactly.

**Runner backoff/park** (mirror SkipWatch `SummaryIngestRunner` and Phase 4's transcript runner): success resets `RetryCount = 0`, `LastError = null`, `NextAttemptAt = null`; failure increments `RetryCount`, records `LastError`, parks at `>= MaxRetryAttempts` (`Parked = true`, `ParkedAt`, `NextAttemptAt = null`) else sets `NextAttemptAt = now + min(1h, 60s × 2^(n-1))`; catch `OperationCanceledException` on the passed token and rethrow (never treat cancellation as failure).

**Worker** (mirror SkipWatch `SummaryWorker`): `ExecuteAsync` while-loop; `TickOnceAsync` opens a scope, pulls one eligible row, runs the runner, returns did-work; `Task.Delay(IdlePollSeconds)` only when idle; swallow per-tick exceptions with a log; break on cancellation.

**Deterministic templating**: the LLM returns **Markdown only** for the body; the concept HTML page is assembled deterministically by `OkfLibraryStore` from `concept.html`. Never write LLM output straight to disk as a page.

**Atomic disk writes** (reuse Phase 2's helper): temp file + move-overwrite, UTF-8 without BOM, filename validated against `^[A-Za-z0-9._-]+$`, written only inside `RootPath\<project-slug>\`.

**Warnings-as-errors**: everything compiles clean under `TreatWarningsAsErrors=true`; unused usings and nullable warnings fail the build.

---

## IMPLEMENTATION PLAN

**Rendering**: Milestones — the phase has natural sub-layers (LLM client → summarizer → concept-page writer → runner/worker → tests) worth surfacing.

**Rationale**: 8 tasks across four distinct layers (LLM transport, summarization, deterministic HTML output, background pipeline) with cross-layer integration (a transcribed row must end as a conformant concept page on disk at `Status = Summarized`). Milestone checkpoints prove integration beyond each task's isolated VALIDATE.

Tasks execute one at a time, top to bottom. Task numbering is contiguous across milestones.

### Milestone 1: LLM client & summarizer

A reusable OpenAI-compatible client and a json_schema-backed video summarizer, both unit-testable with a mocked handler.

**Validation checkpoint**: `dotnet build VideoCortex.slnx` succeeds and `dotnet test VideoCortex.slnx --filter FullyQualifiedName~OpenAiSummarizer` passes with a canned completion (no network).

#### Task 1: OpenAI-compatible client + per-endpoint cache

Create the LLM transport layer under `Services/Llm/`.

- **IMPLEMENT**: `OpenAiClientCache` — `public const string DefaultBaseUrl = "https://api.openai.com"`; `HttpClient Get(string? baseUrl, string? apiKey, int timeoutSeconds)` keyed on `effectiveBase|apiKey|timeoutSeconds` under a lock, rebuilding only on key change, throwing `InvalidOperationException` on blank key, setting `BaseAddress` + `Authorization: Bearer` + `Timeout`; `IDisposable`. `OpenAiClient` — owns one `OpenAiClientCache`, exposes `Task<ChatCompletionResponse> PostChatAsync(LlmSettings settings, ChatCompletionRequest request, CancellationToken ct)` doing `PostAsJsonAsync("/v1/chat/completions", …)`, non-2xx → read body → `HttpRequestException`, deserialize `ChatCompletionResponse`. Put the request/response DTOs (`ChatCompletionRequest`, `ChatMessage`, `ResponseFormat`, `JsonSchemaSpec`, `ChatCompletionResponse`, `ChatChoice`) here as `internal`/nested so both summarizer (this phase) and Phase 6's synthesizer reuse them.
- **PATTERN**: `SkipWatch.Core\Services\OpenAi\OpenAiClientCache.cs`; `OpenAiSummarizer.cs` private DTOs + `PostChatAsync`.
- **IMPORTS**: `System.Net.Http.Headers`, `System.Net.Http.Json`, `System.Text.Json`, `System.Text.Json.Nodes`, `System.Text.Json.Serialization`, `VideoCortex.Core.Services.Config` (`LlmSettings`).
- **GOTCHA**: Do **not** dispose the old client on rebuild (would cancel in-flight calls) — just drop the reference, per the SkipWatch remarks. Read `LlmSettings` values (`Model`/`BaseUrl`/`ApiKey`/timeout) from the record the *caller* passes; the client itself takes no `IOptionsMonitor` — the summarizer reads `CurrentValue` and hands the record in.
- **VALIDATE**: `cd /c/Repos/VideoCortex && dotnet build VideoCortex.Core` exits 0.

#### Task 2: `IVideoSummarizer` + `VideoSummary` + json_schema call

Create the summarizer that turns transcript + title/channel into structured summary fields.

- **IMPLEMENT**: `VideoSummary` — `record` with `[property: JsonPropertyName("title")] string Title`, `("description") string Description`, `("tags") IReadOnlyList<string> Tags`, `("body_markdown") string BodyMarkdown`. `SummaryParseException` (mirror SkipWatch). `IVideoSummarizer.SummarizeAsync(title, channel, transcript, aiInstructions?, ct)`. `OpenAiSummarizer` — depends on `IOptionsMonitor<SummarySettings>` (worker/model knobs) and `IOptionsMonitor<LlmSettings>` (endpoint) + `OpenAiClient` + `ILogger`; builds the system prompt (produce a **complete, faithful** per-video summary — not a triage card; the summary feeds a cross-video report), threads `aiInstructions` into the user prompt when non-blank, builds `ChatCompletionRequest` with the strict json_schema (`additionalProperties:false`, all four props in `required`), calls `OpenAiClient.PostChatAsync`, deserializes the first choice's content into `VideoSummary`, guards null/empty `Title`/`BodyMarkdown` → `SummaryParseException`. `TranscriptWindow.Trim(transcript, …)` applied before the call.
- **PATTERN**: `SkipWatch.Core\Services\Summarization\OpenAiSummarizer.cs` (schema/DTO/prompt shape), `ISummarizer.cs`, `SummaryResponse.cs`, `SummaryParseException.cs`.
- **IMPORTS**: `System.Text`, `System.Text.Json`, `System.Text.Json.Nodes`, `System.Text.Json.Serialization`, `Microsoft.Extensions.Options`, `Microsoft.Extensions.Logging`, `VideoCortex.Core.Services.Config`.
- **GOTCHA**: The schema property set and the `VideoSummary` JSON names must match exactly, or strict mode 400s / deserialization drops fields. `tags` may come back empty — allow it (empty array is valid), but reject empty `title`/`body_markdown`. Model name is read from `LlmSettings.Model` (Phase 1), **not** hardcoded.
- **VALIDATE**: `cd /c/Repos/VideoCortex && dotnet build VideoCortex.Core` exits 0.

#### Task 3: Summarizer unit test (mocked handler, no network)

Prove the summarizer round-trips a canned json_schema completion.

- **IMPLEMENT**: `OpenAiSummarizerTests` — a test `HttpMessageHandler` (or `DelegatingHandler` seam) returning a `200` chat-completion JSON whose single choice's `message.content` is `{"title":"…","description":"…","tags":["a","b"],"body_markdown":"# H\n\ntext"}`. Since `OpenAiClient`/`OpenAiClientCache` construct their own `HttpClient`, add a **test seam**: an internal constructor (or `internal` virtual factory) on `OpenAiClientCache`/`OpenAiClient` that accepts an `HttpMessageHandler`, exposed to tests via `[assembly: InternalsVisibleTo("VideoCortex.Tests")]` on `VideoCortex.Core`. Assert: parsed `VideoSummary` fields match; a `500`/malformed body → `HttpRequestException`/`SummaryParseException`; a blank `ApiKey` in `LlmSettings` → `InvalidOperationException`. Feed settings via `Options.Create(...)` / a test `IOptionsMonitor`.
- **PATTERN**: SkipWatch summarizer tests (mocked `HttpMessageHandler`), Phase 4's Apify-source tests for the handler seam.
- **IMPORTS**: `System.Net`, `System.Net.Http`, `System.Text`, `Xunit`, `FluentAssertions`, `Microsoft.Extensions.Options`, `Microsoft.Extensions.Logging.Abstractions`.
- **GOTCHA**: No real network — the handler must be injected, never a live base URL. Add `InternalsVisibleTo` in the `.csproj` or an `AssemblyInfo`; without a handler seam the cache builds a real client and the test would hit the network.
- **VALIDATE**: `cd /c/Repos/VideoCortex && dotnet test VideoCortex.slnx --filter FullyQualifiedName~OpenAiSummarizer` exits 0.

### Milestone 2: Concept-page writer

`OkfLibraryStore` can render a `VideoSummary` into a conformant OKF concept page on disk.

**Validation checkpoint**: `dotnet test VideoCortex.slnx --filter FullyQualifiedName~ConceptPageWriter` passes — a temp-dir write parses as HTML, carries a valid `okf-meta` with non-empty `type`, links `theme.css` relatively, and its body starts with `<h1>`.

#### Task 4: `WriteConceptPageAsync` on `OkfLibraryStore`

Render Markdown → HTML and fill the concept template deterministically.

- **IMPLEMENT**: On `OkfLibraryStore` (and its interface): `Task<string> WriteConceptPageAsync(Project project, Video video, VideoSummary summary, CancellationToken ct = default)` returning the concept slug. Steps: (1) derive a filesystem-safe, project-unique `conceptSlug` from `summary.Title` (fallback `video.YoutubeVideoId`) via the Phase 2 slug helper, with collision suffixing scoped to the project folder; (2) render `summary.BodyMarkdown` → HTML with a cached `MarkdownPipeline` (`UseAdvancedExtensions()`); (3) load `concept.html` (Phase 2's template loader) and fill placeholders — `{{TITLE}}` (JSON-string-escaped in meta, HTML-escaped in `<title>`/`<h1>`), `{{THEME_HREF}}` = `"theme.css"` (concept at library root, so no `../`), `{{TYPE}}` = `Video`, `{{DESCRIPTION}}` (escaped), `{{TAGS}}` = JSON array *contents* (e.g. `"a", "b"`), `{{RESOURCE}}` = the JSON value `"https://www.youtube.com/watch?v=<id>"` (a quoted string, `null` if no id), `{{TIMESTAMP}}` = `DateTime.UtcNow` ISO 8601 (`"o"`/`yyyy-MM-ddTHH:mm:ssZ`), `{{TAG_CHIPS}}` = `<span class="okf-tag">…</span>` per tag (HTML-escaped), `{{BODY}}` = the Markdig HTML; (4) atomic write to `<RootPath>\<project.Slug>\<conceptSlug>.html`.
- **PATTERN**: Phase 2 `OkfLibraryStore.CreateLibrary` (template load + atomic write + slug); OKF `concept.html`; SPEC §4.
- **IMPORTS**: `Markdig`, `System.Text.Json` (for escaping the meta strings), `System.Text`, `VideoCortex.Core.Entities`, `VideoCortex.Core.Services.Llm`.
- **GOTCHA**: **The LLM never emits page HTML** — only `body_markdown`; the wrapper is deterministic so pages are uniformly themed and always valid. Escape correctly per placeholder context: `okf-meta` values are **JSON string contents** (use `JsonEncodedText`/`JsonSerializer` to escape quotes/backslashes/newlines), while `<title>`/`<h1>`/chips are **HTML text** (escape `<`, `>`, `&`, `"`). `{{TAGS}}` sits inside the template's existing `[…]`, and `{{RESOURCE}}` is a bare JSON value (already unquoted in the template) — do not double-wrap either. Markdig output goes into `{{BODY}}` verbatim (it is already HTML). Ensure the rendered body begins with an `<h1>`: the template already emits `<h1>{{TITLE}}</h1>` above `{{BODY}}`, so per SPEC §4.3 the page's first body heading is the `<h1>` — do not also force an `<h1>` from the Markdown.
- **VALIDATE**: `cd /c/Repos/VideoCortex && dotnet build VideoCortex.slnx` exits 0.

#### Task 5: Concept-page writer test (temp dir, conformance)

Prove the writer emits a conformant OKF concept page.

- **IMPLEMENT**: `ConceptPageWriterTests` — construct an `OkfLibraryStore` pointed at a temp `RootPath` (create a project library first via Phase 2's `CreateLibrary`, or seed the folder + `theme.css`), call `WriteConceptPageAsync` with a `VideoSummary` containing a Markdown body with a heading, list, and code fence, plus tags. Assert: (a) the file exists at `<root>\<slug>\<conceptSlug>.html` and parses as HTML (e.g. via a lightweight parse or regex for well-formedness / no leftover `{{…}}` placeholders); (b) the `okf-meta` `<script>` block parses as JSON and has non-empty `type == "Video"`, correct `title`/`resource`; (c) `theme.css` is linked **relatively** (`href="theme.css"`, no leading `/`); (d) the body contains an `<h1>`; (e) tags render as chips and as a JSON array; (f) a title with unsafe filename chars still yields a valid `^[A-Za-z0-9._-]+$` slug, and two videos with the same title produce distinct filenames. **No network.** Clean up the temp dir in `Dispose`.
- **PATTERN**: OKF SPEC §9 conformance list; Phase 2 library-writer tests.
- **IMPORTS**: `System.IO`, `System.Text.Json`, `System.Text.RegularExpressions`, `Xunit`, `FluentAssertions`.
- **GOTCHA**: Assert JSON validity by actually `JsonDocument.Parse`-ing the extracted `okf-meta` payload, not string-matching — that is the real conformance gate (SPEC §9.2/§9.3). Use `Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())` for isolation.
- **VALIDATE**: `cd /c/Repos/VideoCortex && dotnet test VideoCortex.slnx --filter FullyQualifiedName~ConceptPageWriter` exits 0.

### Milestone 3: Runner & worker

A transcribed video is summarized, its concept page written, and it advances to `Summarized` — with backoff/park on failure.

**Validation checkpoint**: `dotnet test VideoCortex.slnx --filter FullyQualifiedName~SummaryIngestRunner` passes (success → `Summarized` + concept file + fields persisted; failure → retry then park).

#### Task 6: `SummaryIngestRunner`

Per-video orchestration: summarize → write page → persist → advance status, with backoff/park.

- **IMPLEMENT**: `SummaryIngestResult` (record + `SummaryIngestOutcome { Summarized, Retry, Parked }`, mirroring Phase 4). `ISummaryIngestRunner.RunAsync(Video video, CancellationToken ct)`. `SummaryIngestRunner` — depends on `VideoCortexDbContext`, `IVideoSummarizer`, `IOkfLibraryStore`, `IOptions<SummarySettings>` (for `MaxRetryAttempts`), `ILogger`. Flow: guard empty `TranscriptText` → `InvalidOperationException`; load the owning `Project` (for `Name` + `AIInstructions` + `Slug`); `TranscriptWindow.Trim`; `await _summarizer.SummarizeAsync(video.Title ?? "", video.ChannelTitle ?? "", window, project.AIInstructions, ct)`; `var slug = await _store.WriteConceptPageAsync(project, video, summary, ct)`; set `video.SummaryTitle/SummaryDescription/SummaryBodyMd`, `video.ConceptSlug = slug`, `video.SummarizedAt = UtcNow`, `video.Status = VideoStatus.Summarized`, reset `RetryCount=0`/`LastError=null`/`NextAttemptAt=null`; `SaveChangesAsync`. On exception (not cancellation): increment `RetryCount`, set `LastError`, park at `>= MaxRetryAttempts` else exponential backoff `NextAttemptAt`. Rethrow `OperationCanceledException` when the token is cancelled.
- **PATTERN**: `SkipWatch.Core\Services\Summarization\SummaryIngestRunner.cs` (drop `Activity`/`JobEventBus`); Phase 4 transcript runner's backoff/park + result type.
- **IMPORTS**: `System.Diagnostics`, `Microsoft.EntityFrameworkCore`, `Microsoft.Extensions.Options`, `Microsoft.Extensions.Logging`, `VideoCortex.Core.Db`, `VideoCortex.Core.Entities`, `VideoCortex.Core.Services.Llm`, `VideoCortex.Core.Services.Library`.
- **GOTCHA**: Write the concept page **before** flipping `Status = Summarized` — if the write throws, the row stays `Transcribed` and retries; never advance status without a page on disk. Only reset retry state on the success path. Do not double-park an already-parked row (the worker query excludes `Parked`, but keep the guard honest).
- **VALIDATE**: `cd /c/Repos/VideoCortex && dotnet build VideoCortex.slnx` exits 0.

#### Task 7: `SummaryWorker` + DI wiring

Hosted polling service that drains the transcribed queue, plus registrations.

- **IMPLEMENT**: `VideoCortex\Workers\SummaryWorker.cs` — `BackgroundService` mirroring SkipWatch: `ExecuteAsync` loop; `TickOnceAsync` opens a DI scope, queries `db.Videos.Where(v => v.Status == VideoStatus.Transcribed && !v.Parked && (v.NextAttemptAt == null || v.NextAttemptAt <= now)).OrderBy(v => v.NextAttemptAt).ThenBy(v => v.TranscribedAt).FirstOrDefaultAsync(ct)`, runs `ISummaryIngestRunner`, returns did-work; `Task.Delay(IdlePollSeconds)` when idle; log + swallow per-tick exceptions; break on cancellation. `Program.cs` — register `OpenAiClient` (singleton, owns the cache), `IVideoSummarizer → OpenAiSummarizer` (singleton is fine; it reads `IOptionsMonitor`), `ISummaryIngestRunner → SummaryIngestRunner` (scoped — touches `DbContext`), `AddHostedService<SummaryWorker>()`. Confirm `IOkfLibraryStore` is already registered (Phase 2); if not, register it. Ensure `Summary`/`Llm` config sections bind.
- **PATTERN**: `SkipWatch\Services\Workers\SummaryWorker.cs`; Phase 4's `TranscriptWorker` registration in `Program.cs`.
- **IMPORTS**: `Microsoft.EntityFrameworkCore`, `Microsoft.Extensions.Options`, `VideoCortex.Core.Db`, `VideoCortex.Core.Entities`, `VideoCortex.Core.Services.Summary`.
- **GOTCHA**: The runner is **scoped** (uses `DbContext`) — resolve it *inside* the per-tick scope, never inject it into the singleton worker's constructor (captive-dependency bug). The summarizer/client may be singletons since they hold no scoped state. Do not register a second `HttpClient` for the LLM via `IHttpClientFactory` — the cache owns them.
- **VALIDATE**: `cd /c/Repos/VideoCortex && dotnet build VideoCortex.slnx` exits 0.

#### Task 8: `SummaryIngestRunner` tests (in-memory SQLite, fake summarizer)

Cover success, retry, and park without network.

- **IMPLEMENT**: `SummaryIngestRunnerTests` using the Phase 1 in-memory SQLite fixture. Seed a `Project` (temp `RootPath` library) + a `Video(Status=Transcribed, TranscriptText="…")`. With a **fake `IVideoSummarizer`** returning a fixed `VideoSummary`: assert the video reaches `Status=Summarized`, `ConceptSlug` set, `SummaryTitle/Description/BodyMd` persisted, `SummarizedAt` non-null, and the concept file exists on disk. With a fake summarizer that **throws**: assert first failures set `RetryCount` + `NextAttemptAt` (still `Transcribed`), and at `MaxRetryAttempts` the row is `Parked` with `LastError`. Assert an empty-`TranscriptText` row throws `InvalidOperationException`. Use a real `OkfLibraryStore` against a temp dir (writer already covered in Task 5) or a fake `IOkfLibraryStore`. **No network.**
- **PATTERN**: SkipWatch `SummaryIngestRunner` tests; Phase 4 transcript-runner tests.
- **IMPORTS**: `Microsoft.EntityFrameworkCore`, `Microsoft.Extensions.Options`, `Microsoft.Extensions.Logging.Abstractions`, `Xunit`, `FluentAssertions`.
- **GOTCHA**: Keep the fixture's single `:memory:` connection open for the test lifetime (Phase 1 note). Drive backoff/park by lowering `MaxRetryAttempts` in the injected `SummarySettings` so the park path is reached in a couple of iterations.
- **VALIDATE**: `cd /c/Repos/VideoCortex && dotnet test VideoCortex.slnx --filter FullyQualifiedName~SummaryIngestRunner` exits 0.

### Milestone 4: Full-suite validation

Whole phase green; no regressions.

**Validation checkpoint**: `dotnet build VideoCortex.slnx -warnaserror` and `dotnet test VideoCortex.slnx` both pass.

#### Task 9: Full build + test sweep

- **IMPLEMENT**: Run the full validation commands below; fix any warnings-as-errors fallout (unused usings, nullable) and any cross-phase breakage introduced by the `OkfLibraryStore`/`Program.cs`/`.csproj` edits.
- **PATTERN**: Phase 1 validation levels.
- **IMPORTS**: n/a.
- **VALIDATE**: `cd /c/Repos/VideoCortex && dotnet build VideoCortex.slnx -warnaserror && dotnet test VideoCortex.slnx` exits 0.

### Milestone 5: Commit, push, and open PR (mandatory)

**Validation checkpoint**: Branch pushed to origin; PR open against `main`.

#### Task 10: Commit, push, and open PR

- **IMPLEMENT**:
  - Create/switch to branch `phase-5-summary` off `main`.
  - Stage and commit all changes: message `Phase 5: Summary → concept page — OpenAI-compatible client, video summarizer, OKF concept-page writer, summary worker`.
  - Push: `git push -u origin phase-5-summary`.
  - Open PR: `gh pr create --base main --head phase-5-summary --title "Phase 5: Summary" --body "<body>"` where `<body>` is the ACCEPTANCE CRITERIA as a checked checklist + a `## Notes` section.
- **GOTCHA**: **If no `origin` remote exists or `gh` is unauthenticated** (`git remote -v` empty / `gh auth status` fails), stop after the local commit and report that push/PR require a remote — do not fail silently and do not invent a remote. Do not add Claude as a commit author or `Co-Authored-By` (repo convention).
- **VALIDATE**: `gh pr view --json number,title,state,headRefName` returns the PR with `"state":"OPEN"` and `"headRefName":"phase-5-summary"` (only runnable once a remote exists).

---

## TESTING STRATEGY

### Unit Tests
- `OpenAiSummarizer` round-trips a canned json_schema completion via a mocked `HttpMessageHandler`; non-2xx → `HttpRequestException`; malformed/empty content → `SummaryParseException`; blank `ApiKey` → `InvalidOperationException`.
- `OkfLibraryStore.WriteConceptPageAsync` emits an OKF-conformant concept page (valid `okf-meta` JSON, non-empty `type`, relative `theme.css`, body starts with `<h1>`, no leftover placeholders); slug is filesystem-safe and project-unique.
- `TranscriptWindow.Trim` bounds long transcripts per the documented window.

### Integration Tests
- `SummaryIngestRunner` on in-memory SQLite with a fake summarizer: success → `Summarized` + concept file + persisted fields; injected failure → retry (backoff, still `Transcribed`) then park (`Parked`, `LastError`); empty transcript → `InvalidOperationException`.

### Edge Cases
- LLM returns empty `tags` (valid) vs. empty `title`/`body_markdown` (rejected).
- Title with unsafe characters or empty title → slug falls back to video id and stays `^[A-Za-z0-9._-]+$`.
- Two videos with identical titles → distinct concept filenames (collision suffix).
- Endpoint config edited mid-run → next call uses the new client (cache rebuild).
- Cancellation during a call → `OperationCanceledException` rethrown, row not parked.
- Concept-page write failure → row stays `Transcribed` and retries (status never advanced without a page on disk).

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
cd /c/Repos/VideoCortex && dotnet test VideoCortex.slnx --filter "FullyQualifiedName~OpenAiSummarizer|FullyQualifiedName~ConceptPageWriter"
```
**Expected**: exit 0; summarizer + concept-writer tests green, no network.

### Level 3: Integration Tests
```bash
cd /c/Repos/VideoCortex && dotnet test VideoCortex.slnx --filter FullyQualifiedName~SummaryIngestRunner
```
**Expected**: exit 0; success/retry/park paths verified.

### Level 4: Full Suite
```bash
cd /c/Repos/VideoCortex && dotnet test VideoCortex.slnx
```
**Expected**: exit 0; all phases' tests pass (no regressions from the `OkfLibraryStore`/`Program.cs`/`.csproj` edits).

---

## ACCEPTANCE CRITERIA

- [ ] `OpenAiClientCache` + `OpenAiClient` exist under `Services/Llm/`, target any OpenAI-compatible `/v1/chat/completions` endpoint, honor `LlmSettings` (blank `BaseUrl` → hosted OpenAI; Ollama `/v1` supported), and rebuild the `HttpClient` on config change via `IOptionsMonitor` — no restart needed.
- [ ] `IVideoSummarizer` / `OpenAiSummarizer` issue a single `response_format=json_schema` call returning `{ title, description, tags[], body_markdown }`, thread the project's `AIInstructions`, and reject empty title/body.
- [ ] `OkfLibraryStore.WriteConceptPageAsync` renders `body_markdown` via Markdig and fills the OKF concept template deterministically (LLM never emits page HTML); output is OKF-v0.1-conformant (valid `okf-meta`, non-empty `type` = "Video", relative `theme.css`, body starts with `<h1>`); `ConceptSlug` is filesystem-safe and project-unique.
- [ ] Video fields (`SummaryTitle`/`Description`/`BodyMd`, `ConceptSlug`, `SummarizedAt`) persist and the video advances to `Status = Summarized` only after its concept page is on disk.
- [ ] `SummaryIngestRunner` + hosted `SummaryWorker` drain the `Transcribed` queue with exponential backoff + parking on repeated failure; the runner is resolved per-scope (no captive `DbContext`).
- [ ] Markdig added to `VideoCortex.Core`; DI wired; `Summary`/`Llm` config sections bind with blank secrets.
- [ ] All validation commands pass; tests green; no network in tests.

---

## COMPLETION CHECKLIST

- [ ] All tasks completed in order
- [ ] Each task validation passed immediately
- [ ] Level 1: `dotnet build -warnaserror` clean
- [ ] Level 2/3/4: `dotnet test` green (summarizer, concept-writer, runner, full suite)
- [ ] All acceptance criteria met
- [ ] Branch `phase-5-summary` pushed and PR opened against `main` (final task; requires remote)

---

## NOTES

- **Base branch is `main`**, not `master`. Branch: `phase-5-summary` off `main`. Commits authored solely by the user — no `Co-Authored-By: Claude`.
- **No SkipWatch dependency**: SkipWatch is a read-only pattern reference. Video Cortex shares no code or project reference with it. Notably, this phase drops SkipWatch's `Activity`/`ActivityEntry` logging, `JobEventBus`, `Channel` navigation, and `decision_signal` — none exist here.
- **Transcript window (design decision to document in code):** SkipWatch's `TranscriptSlicer.TakeFirstMinutes` keeps only the first ~5 minutes because it produces a *triage card*. Video Cortex's summary feeds Phase 6's cross-video **report**, so it must be reasonably **complete** — summarize the whole transcript. Implement `TranscriptWindow.Trim` as a generous **character-bounded** guard (e.g. cap at a large budget sized to the model's context, defaulting effectively to "no trim" for typical videos) rather than a 5-minute cut; only very long transcripts get trimmed, and even then keep head + tail rather than head-only. Chunked/map-reduce summarization for extreme outliers is explicitly a **v2** concern (PRD §13, Risks). Record the chosen budget and rationale in the `TranscriptWindow` XML doc-comment.
- **Deterministic HTML is the core safety property (PRD §13):** the LLM returns Markdown for the body only; the page shell, `okf-meta`, meta-bar, and `<h1>` are filled by `OkfLibraryStore` from `wwwroot/okf/concept.html`. This guarantees valid, uniformly-themed pages regardless of model quality, and is what makes OKF conformance testable.
- **Placeholder-escaping contract (Task 4):** the template pre-supplies `[…]` around `{{TAGS}}` and leaves `{{RESOURCE}}` as a bare JSON value — fill `{{TAGS}}` with array *contents* only and `{{RESOURCE}}` with a quoted string or `null`, never double-wrapped. `okf-meta` string values are JSON-escaped; `<title>`/`<h1>`/chip text are HTML-escaped. Getting this wrong breaks `okf-meta` JSON parsing (the conformance gate) — Task 5 asserts it by actually `JsonDocument.Parse`-ing the block.
- **No new migration**: every column this phase writes (`SummaryTitle`, `SummaryDescription`, `SummaryBodyMd`, `ConceptSlug`, `SummarizedAt`, plus the retry/park columns) was created in the Phase 1 initial migration. If any is missing, that is a Phase 1 gap — surface it, don't silently add a migration here.
- **`InternalsVisibleTo`**: the summarizer test needs a handler seam into `OpenAiClient`/`OpenAiClientCache`. Add `[assembly: InternalsVisibleTo("VideoCortex.Tests")]` (or `<InternalsVisibleTo>` in the `.csproj`) on `VideoCortex.Core`, mirroring however Phase 4 exposed its Apify handler seam — reuse that mechanism rather than inventing a second one.
- **DI lifetimes**: `OpenAiClient` + `IVideoSummarizer` are singletons (they hold only `IOptionsMonitor` + the client cache); `ISummaryIngestRunner` is **scoped** (touches `DbContext`) and must be resolved inside the worker's per-tick scope.
- **Remote/PR precondition**: if no `origin` remote exists or `gh` is unauthenticated at execution time, the phase ends at a local commit; report that push + PR require adding a remote and authenticating `gh`.
