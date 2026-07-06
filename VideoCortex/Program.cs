using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VideoCortex.Components;
using VideoCortex.Core.Db;
using VideoCortex.Core.Services.Config;
using VideoCortex.Core.Services.Library;
using VideoCortex.Core.Services.Llm;
using VideoCortex.Core.Services.Report;
using VideoCortex.Core.Services.Summary;
using VideoCortex.Core.Services.Transcripts;
using VideoCortex.Core.Services.Triage;
using VideoCortex.Features.Projects.Services;
using VideoCortex.Features.Settings.Services;
using VideoCortex.Services;
using VideoCortex.Workers;

var builder = WebApplication.CreateBuilder(args);

VideoCortexPaths.EnsureDataDir();

// Writable overlay layered on top of the standard chain (appsettings.json → user-secrets
// in Development → environment variables). Phase 7's Settings page persists tunables and
// credentials here; reloadOnChange lets IOptionsMonitor consumers pick up edits without a restart.
builder.Configuration.AddJsonFile(
    VideoCortexPaths.OverlayPath, optional: true, reloadOnChange: true);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDbContext<VideoCortexDbContext>(o =>
    o.UseSqlite($"Data Source={VideoCortexPaths.DbPath}"));

builder.Services.Configure<ApifySettings>(builder.Configuration.GetSection(ApifySettings.Section));
builder.Services.Configure<LlmSettings>(builder.Configuration.GetSection(LlmSettings.Section));
builder.Services.Configure<LibrarySettings>(builder.Configuration.GetSection(LibrarySettings.Section));
builder.Services.Configure<TranscriptWorkerSettings>(builder.Configuration.GetSection(TranscriptWorkerSettings.Section));
builder.Services.Configure<SummarySettings>(builder.Configuration.GetSection(SummarySettings.Section));
builder.Services.Configure<ReportSettings>(builder.Configuration.GetSection(ReportSettings.Section));

// OKF library disk-writer. Singleton constructed from the configured library root and the
// bundled wwwroot/okf templates. RootPath is captured at startup (static this phase; Phase 7's
// Settings page can revisit if it becomes runtime-editable). Create the root defensively —
// the default Documents\SecondBrain may not exist on a fresh machine.
builder.Services.AddSingleton<IOkfLibraryStore>(sp =>
{
    var root = sp.GetRequiredService<IOptions<LibrarySettings>>().Value.RootPath;
    Directory.CreateDirectory(root);
    var templatesDir = Path.Combine(sp.GetRequiredService<IWebHostEnvironment>().WebRootPath, "okf");
    return new OkfLibraryStore(root, templatesDir);
});
builder.Services.AddScoped<IProjectService, ProjectService>();

// Writable config overlay + settings service. Overlay is a singleton so its write-serializing
// SemaphoreSlim is shared. Edits land in %USERPROFILE%\.videocortex\appsettings.Local.json,
// which is layered with reloadOnChange — so LLM/endpoint changes are live without a restart.
builder.Services.AddSingleton<IOverlayWriter>(_ => new OverlayWriter(VideoCortexPaths.OverlayPath));
builder.Services.AddScoped<ISettingsService, SettingsService>();

builder.Services.AddScoped<IVideoCommands, VideoCommands>();
builder.Services.AddScoped<IVideoQueries, VideoQueries>();
builder.Services.AddScoped<IVideoRetryCommand, VideoRetryCommand>();
builder.Services.AddSingleton<IDesktopFileOpener, DesktopFileOpener>();

// Transcript pipeline stage: typed HttpClient source (Apify) + scoped per-video runner +
// the polling background worker. The typed-client registration also registers ITranscriptSource.
builder.Services.AddHttpClient<ITranscriptSource, ApifyTranscriptSource>();
builder.Services.AddScoped<ITranscriptIngestRunner, TranscriptIngestRunner>();
builder.Services.AddHostedService<TranscriptWorker>();

// Summary pipeline stage: the OpenAI-compatible client + summarizer are singletons (they hold
// only IOptionsMonitor + the endpoint client cache); the runner is scoped (touches DbContext).
builder.Services.AddSingleton<OpenAiClient>();
builder.Services.AddSingleton<IVideoSummarizer, OpenAiSummarizer>();
builder.Services.AddScoped<ISummaryIngestRunner, SummaryIngestRunner>();
builder.Services.AddHostedService<SummaryWorker>();

// Report pipeline stage (terminal): synthesizer (reuses the singleton OpenAiClient) + the scoped
// per-project regeneration runner + the debounced worker.
builder.Services.AddScoped<IReportSynthesizer, OpenAiReportSynthesizer>();
builder.Services.AddScoped<IReportRegenerationRunner, ReportRegenerationRunner>();
builder.Services.AddHostedService<ReportWorker>();

var app = builder.Build();

// Apply migrations at startup so the SQLite file and schema exist on first run.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<VideoCortexDbContext>();
    db.Database.Migrate();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
