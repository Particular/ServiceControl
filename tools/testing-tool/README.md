# ServiceControl Testing Tool

A stateless, horizontally-scalable .NET 10 service that generates error load and real-world failure
scenarios against a test ServiceControl instance, with OpenTelemetry observability and a simple web
UI for manual scenario control.

See [requirements-test-tool-plan.md](./requirements-test-tool-plan.md) for the full plan.

## Status

All phases complete (0–7): project bootstrap, OTel foundation (traces + metrics + logs),
NServiceBus error path (handler + direct error-queue bypass writer), scenarios, background jobs,
web UI, containerization & scaling, Aspire AppHost, and smoke tests. A prebuilt Grafana
dashboard is left for a future iteration (otel traces/metrics already flow to the collector).

## What it does

The tool runs an NServiceBus endpoint (`TestingTool.Load`) that sends messages through a handler
which fails based on the active scenario. Failed messages are routed to the `error` queue for
ServiceControl to ingest. Five scenarios are built in, each producing naturally-grouped errors:

| Scenario | Category | Failure shape |
|---|---|---|
| `third-party-outage` | Outage | 100% fail for 20s bursts, 30s cooldown — grouped by downstream host |
| `timeout-spike` | Timeout | Oscillating 10–70% fail rate — grouped by 5-min batch bucket |
| `poison-message` | Poison | 15% deterministic always-fail messages — retry storm |
| `deserialization-failure` | Deserialization | 100% fail — grouped by message type (bad deployment) |
| `background-noise` | Noise | ~3% always-on baseline — rotates through exception types |

Two background jobs (gated by config) run on timers:
- **Replay** — fetches error groups from ServiceControl and triggers retry
- **Search** — runs canned FTS queries to exercise the RavenDB search index

All telemetry is exported via OTLP (traces + metrics + logs) and a Prometheus `/metrics` endpoint.

### Direct error-queue bypass writer

In addition to the handler path, the tool can write failed-message envelopes directly to the
ServiceControl error queue, bypassing the handler entirely for high-throughput error load.
Each message carries standard NServiceBus failure headers (`NServiceBus.ExceptionInfo.*`,
`NServiceBus.FailedQ`) so ServiceControl ingests it as a genuine failed message. Control via:
- `POST /api/bypass/start` — `{ "scenario": "third-party-outage", "rate": 100, "durationSeconds": 60 }`
- `POST /api/bypass/stop`
- `GET /api/bypass/status`

### Release-test scenario presets

The tool ships with presets mapped from `docs/testing-scenarios.md` so release-test scenarios can
be kicked off manually by name:
- `GET /api/release-tests` — list all presets
- `POST /api/release-tests/{name}/start` — start a preset (e.g. `retry-message-group`, `ingestion-load`)

## Layout

```
tools/testing-tool/
  TestingTool.slnx
  Directory.Build.props          # repo-style conventions (warnings-as-errors, nullable, analyzers)
  Directory.Packages.props       # central package management (OTel + NServiceBus pinned)
  Dockerfile
  global.json
  TestingTool/                   # ASP.NET Core host: Program.cs, web UI, services
    TestingTool.csproj
    Program.cs                   # OTel wiring, NServiceBus endpoint, DI, API endpoints
    appsettings.json             # base config (TestingTool section, overridable by env vars)
    appsettings.Development.json # Development overrides
    wwwroot/index.html           # single-page web UI (vanilla JS, no build step)
    ScenarioRunner.cs            # start/stop, rate control, per-scenario error counting
    DirectErrorQueueWriter.cs    # bypass path: writes failed-message envelopes directly to error queue
    FailingMessageHandler.cs     # NServiceBus handler that throws per scenario logic
    ReleaseTestScenarios.cs      # release-test preset mappings (Phase 5)
    ServiceControlClient.cs      # REST API client (error groups, replay, search)
    ReplayService.cs             # background replay job
    SearchService.cs             # background search job
    TelemetrySetup.cs            # OTel traces + metrics + logs + OTLP/Prometheus exporters
    NServiceBusSetup.cs          # endpoint config (Learning transport, error queue routing)
    TestingToolOptions.cs        # config (SC URL, replay/search intervals, error queue name)
    TestingToolMetrics.cs        # shared live counters for /api/status
    ShardIdResolver.cs           # shard id from env var, StatefulSet ordinal, or hostname
    IScenarioRegistry.cs         # scenario registry abstraction (DI)
    ScenarioRegistry.cs          # default scenario registry implementation
  TestingTool.Scenarios/         # IScenario contract + 5 scenario implementations
  TestingTool.Contracts/         # shared DTOs (ScenarioInfo, TestingToolStatus, BypassStatus, etc.)
  TestingTool.SmokeTests/        # xunit smoke tests (requires running SC + tool)
  TestingTool.AppHost/           # Aspire AppHost project (platform + tool + Jaeger)
```

## Run locally

```bash
dotnet build tools/testing-tool/TestingTool.slnx --configuration Release
dotnet run --project tools/testing-tool/TestingTool --configuration Release
```

Open http://localhost:5290 (or the port shown in the console).

## Run with Aspire

The Aspire AppHost orchestrates the testing tool together with the full Particular platform
(ServiceControl + Learning transport + RavenDB + ServicePulse) and Jaeger, so a single command
brings up the whole system locally:

```bash
aspire run tools/testing-tool/TestingTool.AppHost/TestingTool.AppHost.csproj
```

To test a specific ServiceControl image tag (e.g. a PR-based prerelease tag):

```bash
aspire run tools/testing-tool/TestingTool.AppHost/TestingTool.AppHost.csproj -- pr-1234
```

The Aspire dashboard provides allocated ports for each service. The testing tool automatically
connects to ServiceControl via the platform's transport and REST API URL.

## Run smoke tests

The smoke tests require a running ServiceControl + testing tool (e.g. via the Aspire AppHost above,
or `dotnet run` against an existing ServiceControl):

```bash
# Start the stack first (see Run with Aspire)
dotnet test tools/testing-tool/TestingTool.SmokeTests
```

The test URLs default to `http://localhost:8080` (tool) and `http://localhost:33333` (ServiceControl).
Override them to match your run — Aspire assigns dynamic ports, shown in the Aspire dashboard:
```bash
TESTING_TOOL_URL=http://localhost:<tool-port> SERVICECONTROL_URL=http://localhost:<sc-port> \
  dotnet test tools/testing-tool/TestingTool.SmokeTests
```

## Horizontal scaling

The tool is **stateless** — all state is in-memory per replica. The repo no longer ships
docker-compose or Kubernetes manifests; run a single instance via `dotnet run` or the Aspire
AppHost. For multi-replica deployments, bring your own orchestration and give each replica a
distinct shard id so deterministic failure decisions don't overlap:

| Shard id source | When |
|---|---|
| `SHARD_ID` env var | Explicit override — recommended for any custom deployment |
| Hostname trailing ordinal (e.g. `testing-tool-2` → `2`) | StatefulSet-style ordered hostnames |
| `MachineName` | Fallback — unique per host/pod |

To achieve a target aggregate rate of R msg/s across N replicas, set each replica's scenario rate
to R/N. The web UI and `/api/status` endpoint report per-replica counters; aggregate across
replicas via Prometheus queries or the OTLP backend.

## Configuration

All configuration is via environment variables (no files, no database). Settings are in
`appsettings.json` under the `TestingTool` section, overridable by environment variables using
`__` as the section separator (e.g. `TestingTool__ServiceControlApiUrl`):

| Setting | Default | Description |
|---|---|---|
| `TestingTool__ServiceControlApiUrl` | `http://localhost:33333` | ServiceControl REST API base URL |
| `TestingTool__ReplayEnabled` | `false` | Enable the background replay job |
| `TestingTool__ReplayInterval` | `00:02:00` | Interval between replay cycles |
| `TestingTool__ReplayMinGroupSize` | `1` | Min messages in a group before replaying |
| `TestingTool__SearchEnabled` | `false` | Enable the background search job |
| `TestingTool__SearchInterval` | `00:01:00` | Interval between search cycles |
| `TestingTool__ErrorQueueName` | `error` | NServiceBus error queue (ServiceControl monitors this) |
| `TestingTool__AutoStartBackgroundNoise` | `false` | Auto-start the background-noise scenario on startup |
| `SHARD_ID` (env) | *(auto: hostname ordinal or machine name)* | Shard id for disjoint scenario slices when scaled |
| `OTEL_EXPORTER_OTLP_ENDPOINT` (env) | `http://localhost:4317` | OTLP collector endpoint |
| `OTEL_SERVICE_NAME` (env) | `testing-tool` | OTel service name |

## Health checks

| Endpoint | Purpose |
|---|---|
| `GET /health/live` | Liveness — process is alive |
| `GET /health/ready` | Readiness — app is ready to serve requests |
| `GET /api/status` | Full status snapshot (counters, shard, uptime) |
| `GET /metrics` | Prometheus scraping endpoint |