using Microsoft.EntityFrameworkCore;
using VideoCortex.Components;
using VideoCortex.Core.Db;
using VideoCortex.Core.Services.Config;

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
