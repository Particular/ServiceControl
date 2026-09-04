using System.Diagnostics.Metrics;
using TestingTool;
using TestingTool.Contracts;
using TestingTool.Jobs;
using TestingTool.Scenarios;

// --- Configuration ---

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<TestingToolOptions>(builder.Configuration.GetSection("TestingTool"));

var options = builder.Configuration.GetSection("TestingTool").Get<TestingToolOptions>() ?? new TestingToolOptions();
var shardId = ShardIdResolver.Resolve();

// --- OpenTelemetry (Phase 1) ---

var meter = TelemetrySetup.CreateMeter();
builder.Services.AddTestingToolTelemetry(meter);
builder.Services.AddSingleton(meter);

// --- NServiceBus endpoint (Phase 2) ---

builder.Services.AddTestingToolEndpoint(options, builder.Configuration);

// --- Scenarios (Phase 3) ---

builder.Services.AddSingleton<IScenario>(_ => new ThirdPartyOutageScenario(shardId));
builder.Services.AddSingleton<IScenario>(_ => new TimeoutSpikeScenario(shardId));
builder.Services.AddSingleton<IScenario>(_ => new PoisonMessageScenario(shardId));
builder.Services.AddSingleton<IScenario>(_ => new DeserializationScenario(shardId));
builder.Services.AddSingleton<IScenario>(_ => new RandomBackgroundNoiseScenario(shardId));

builder.Services.AddSingleton<IScenarioRegistry, ScenarioRegistry>();
builder.Services.AddSingleton<ScenarioRunner>();
builder.Services.AddSingleton<DirectErrorQueueWriter>();
builder.Services.AddSingleton<TestingToolMetrics>();

// --- Recoverability/search jobs (Phase 4) ---
// The retry, archive and search jobs used to be hidden config-gated background timers. They are
// now UI-controllable jobs managed by JobRunner and exposed through /api/jobs — start/stop them
// from the web UI. Intervals/minimums still come from configuration as defaults.

builder.Services.AddHttpClient<ServiceControlClient>((sp, client) =>
{
    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<TestingToolOptions>>().Value;
    client.BaseAddress = new Uri(opts.ServiceControlApiUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddSingleton<JobBase, RetryJob>();
builder.Services.AddSingleton<JobBase, ArchiveJob>();
builder.Services.AddSingleton<JobBase, SearchJob>();
builder.Services.AddSingleton<JobBase, RetentionSweepJob>();
builder.Services.AddSingleton<JobBase, CustomCheckFailureJob>();
builder.Services.AddSingleton<JobRunner>();

// --- Application pipeline ---

var app = builder.Build();
app.UseStaticFiles();
app.UseOpenTelemetryPrometheusScrapingEndpoint("/metrics");

var metrics = app.Services.GetRequiredService<TestingToolMetrics>();
var scClient = app.Services.GetRequiredService<ServiceControlClient>();
var jobRunner = app.Services.GetRequiredService<JobRunner>();
var startedAt = DateTimeOffset.UtcNow;

// Auto-start background noise after the endpoint is ready.
app.Lifetime.ApplicationStarted.Register(() =>
{
    if (options.AutoStartBackgroundNoise)
    {
        var runner = app.Services.GetRequiredService<ScenarioRunner>();
        runner.TryStart("background-noise", null, null, out _);
    }
});

// Graceful shutdown: stop all scenarios, jobs and bypass writer.
app.Lifetime.ApplicationStopping.Register(() =>
{
    var runner = app.Services.GetRequiredService<ScenarioRunner>();
    runner.StopAll();
    jobRunner.StopAll();
    app.Services.GetRequiredService<DirectErrorQueueWriter>().Stop();
});

// --- Health endpoints (Phase 6) ---
// Lightweight probe targets for Kubernetes liveness/readiness and docker-compose health checks.
// The chiseled runtime image has no shell/curl, so HTTP probes are used instead of exec probes.

app.MapGet("/health/live", () => Results.Ok(new { status = "alive", shardId }));

app.MapGet("/health/ready", () => Results.Ok(new { status = "ready", shardId }));

// --- API endpoints (Phase 5) ---

app.MapGet("/api/scenarios", (ScenarioRunner runner) => Results.Ok(runner.GetSnapshot()));

app.MapGet("/api/status", () => Results.Ok(new TestingToolStatus
{
    Ready = true,
    ErrorsSent = metrics.TotalErrorsSent,
    ErrorsReplayed = metrics.TotalErrorsReplayed,
    ErrorsArchived = metrics.TotalErrorsArchived,
    SearchesExecuted = metrics.TotalSearches,
    RetentionSweepsStarted = metrics.TotalRetentionSweepsStarted,
    RetentionSweepsAlreadyRunning = metrics.TotalRetentionSweepsAlreadyRunning,
    RetentionSweepsNotSupported = metrics.TotalRetentionSweepsNotSupported,
    BypassErrorsWritten = metrics.TotalBypassErrorsWritten,
    BypassErrorsFailed = metrics.TotalBypassErrorsFailed,
    CustomCheckFailures = metrics.TotalCustomCheckFailures,
    ShardId = shardId,
    ActiveScenarios = metrics.ActiveScenarios,
    ActiveJobs = jobRunner.GetSnapshot().Count(j => j.Running),
    CurrentRate = Math.Round(metrics.CurrentRate, 1),
    ServiceControlUrl = scClient.BaseUrl,
    Uptime = (DateTimeOffset.UtcNow - startedAt).ToString(@"h\h\ m\m\ s\s")
}));

app.MapPost("/api/scenarios/{name}/start", (string name, StartScenarioRequest? request, ScenarioRunner runner) =>
{
    var duration = request?.DurationSeconds is { } secs and > 0
        ? TimeSpan.FromSeconds(secs)
        : (TimeSpan?)null;

    if (!runner.TryStart(name, request?.Rate, duration, out var error))
        return Results.BadRequest(new { error });

    var snapshot = runner.GetSnapshot()
        .First(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    return Results.Ok(snapshot);
});

app.MapPost("/api/scenarios/{name}/stop", (string name, ScenarioRunner runner) =>
{
    if (!runner.TryStop(name))
        return Results.BadRequest(new { error = $"Scenario '{name}' is not running" });

    return Results.Ok(new { stopped = name });
});

app.MapPost("/api/scenarios/stop-all", (ScenarioRunner runner) =>
{
    runner.StopAll();
    return Results.Ok(new { stopped = "all" });
});

// --- Job endpoints (recoverability + search) ---
// These replace the hidden background timers. Jobs are started/stopped on demand from the UI.

app.MapGet("/api/jobs", () => Results.Ok(jobRunner.GetSnapshot()));

app.MapPost("/api/jobs/{name}/start", (string name, StartJobRequest? request) =>
{
    var interval = request?.IntervalSeconds is { } secs and > 0
        ? TimeSpan.FromSeconds(secs)
        : (TimeSpan?)null;

    if (!jobRunner.TryStart(name, interval, out var error))
        return Results.BadRequest(new { error });

    var snapshot = jobRunner.GetSnapshot()
        .First(j => j.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    return Results.Ok(snapshot);
});

app.MapPost("/api/jobs/{name}/stop", (string name) =>
{
    if (!jobRunner.TryStop(name))
        return Results.BadRequest(new { error = $"Job '{name}' is not running" });

    return Results.Ok(new { stopped = name });
});

app.MapPost("/api/jobs/stop-all", () =>
{
    jobRunner.StopAll();
    return Results.Ok(new { stopped = "all" });
});

// --- Bypass endpoints (Phase 2: direct error-queue writer) ---
// These endpoints control the bypass path that writes failed-message envelopes directly to the
// ServiceControl error queue, bypassing the handler for high-throughput error load.

app.MapGet("/api/bypass/status", (DirectErrorQueueWriter writer) => Results.Ok(writer.GetStatus()));

app.MapPost("/api/bypass/start", (StartBypassRequest? request, DirectErrorQueueWriter writer, IScenarioRegistry registry) =>
{
    var scenarioName = request?.Scenario;
    if (string.IsNullOrWhiteSpace(scenarioName))
    {
        // Default to the first scenario if none specified.
        scenarioName = registry.All[0].Name;
    }

    var rate = request?.Rate ?? 100;
    var duration = request?.DurationSeconds is { } secs and > 0
        ? TimeSpan.FromSeconds(secs)
        : (TimeSpan?)null;

    if (!writer.TryStart(scenarioName, rate, duration, request?.Parallelism, out var error))
        return Results.BadRequest(new { error });

    return Results.Ok(writer.GetStatus());
});

app.MapPost("/api/bypass/stop", (DirectErrorQueueWriter writer) =>
{
    writer.Stop();
    return Results.Ok(writer.GetStatus());
});

// --- Release-test scenario endpoints (Phase 5: release-test presets) ---
// Lists release-test presets that map to testing-tool scenarios, and allows kicking them off
// by release-test name. This satisfies the optional requirement to consider release-test
// scenarios for manual kickoff.

app.MapGet("/api/release-tests", () => Results.Ok(ReleaseTestScenarios.Presets.Select(p => new
{
    name = p.Name,
    scenario = p.ScenarioName,
    description = p.Description,
    rate = p.Rate,
    durationSeconds = p.DurationSeconds
})));

app.MapPost("/api/release-tests/{name}/start", (string name, ScenarioRunner runner) =>
{
    var preset = ReleaseTestScenarios.Find(name);
    if (preset is null)
        return Results.BadRequest(new { error = $"Unknown release-test scenario '{name}'" });

    var duration = preset.DurationSeconds is { } secs and > 0
        ? TimeSpan.FromSeconds(secs)
        : (TimeSpan?)null;

    if (!runner.TryStart(preset.ScenarioName, preset.Rate, duration, out var error))
        return Results.BadRequest(new { error });

    return Results.Ok(new { started = preset.Name, scenario = preset.ScenarioName, rate = preset.Rate });
});

app.MapFallbackToFile("index.html");

app.Run();