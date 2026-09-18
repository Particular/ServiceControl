# ServiceControl Testing Tool

A stateless, horizontally-scalable .NET 10 service that generates error load and real-world failure
scenarios against a test ServiceControl instance to validate its error-ingestion performance, with
OpenTelemetry observability and a simple web UI for manual scenario control.

The tool targets the error ingestion path only — audit testing is out of scope.

## What it does

The tool runs an NServiceBus endpoint (`TestingTool.Load`, Learning transport) that sends messages
through a handler which fails based on the active scenario. Failed messages are routed to the
`error` queue for ServiceControl to ingest. Each scenario throws a tagged exception
(`ExceptionType` + `CorrelationGroup`) so ServiceControl groups the failures naturally, and
immediate retries are disabled so groups stay clean. Five scenarios are built in:

| Scenario | Category | Failure shape |
|---|---|---|
| `third-party-outage` | Outage | 100% fail for 20s bursts, 30s cooldown — grouped by downstream host |
| `timeout-spike` | Timeout | Oscillating 10–70% fail rate — grouped by 5-min batch bucket |
| `poison-message` | Poison | 15% deterministic always-fail messages — retry storm |
| `deserialization-failure` | Deserialization | 100% fail — grouped by message type (bad deployment) |
| `background-noise` | Noise | ~3% always-on baseline — rotates through exception types |

Scenarios are controlled from the web UI, or via:
- `GET /api/scenarios` — list scenarios with live status (category, rate, error counts)
- `POST /api/scenarios/{name}/start` — `{ "rate": 100, "durationSeconds": 60 }`; `rate` defaults
  to the scenario's default, `durationSeconds` omitted (or `0`) means run until explicitly stopped
- `POST /api/scenarios/{name}/stop`
- `POST /api/scenarios/stop-all`

Recoverability/search jobs are controllable from the web UI. They run a cycle on a
configurable interval until stopped:
- **Retry** — fetches error groups from ServiceControl and retries each group; replayed messages
  return to the tool's endpoint and succeed (simulating a fix being applied), exercising
  ServiceControl's retry pipeline
- **Archive** — fetches error groups from ServiceControl and archives each group
- **Search** — runs canned FTS queries to exercise the ServiceControl search index
- **Retention sweep** — triggers a manual retention purge on ServiceControl each cycle
  (`POST /api/maintenance/retention/purge`), exercising the retention pipeline (full
  scan-and-delete of aged failures and event-log rows) against the load the other jobs
  produce. Each purge's cutoffs default to ServiceControl's configured retention periods;
  supply a cutoff timespan when starting the job (a field on the job card in the UI, or
  `cutoffTimespan` in the start request) to purge everything older than that instead
- **Custom check failures** — randomly reports internal-looking ServiceControl custom check
  failures to ServiceControl each cycle. Each cycle it sends a `ReportCustomCheckResult`
  per check in a pool of plausibly-named internal checks (category `ServiceControl Health`),
  randomly marking some as failed and the rest as passed, with `EndpointName` set to the
  ServiceControl instance name so they appear in ServicePulse as genuine internal custom
  checks. This exercises ServiceControl's custom-check ingestion and the ServicePulse Custom
  Checks dashboard under failure load, complementing the error-load scenarios.

Jobs do not auto-start; start them from the UI (or `/api/jobs`) when needed. Control via:
- `GET /api/jobs` — list jobs with live status
- `POST /api/jobs/{name}/start` — `{ "intervalSeconds": 120 }` (omit for the job default).
  The retention-sweep job also accepts `{ "cutoffTimespan": "14.00:00:00" }` (.NET timespan
  format) to send explicit purge cutoffs — rows older than `now − timespan` are deleted
  on every sweep; omit it to let ServiceControl derive cutoffs from its configured
  retention periods
- `POST /api/jobs/{name}/stop`
- `POST /api/jobs/stop-all`

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

The tool ships with presets mapped from [docs/testing-scenarios.md](/docs/testing-scenarios.md) so
release-test scenarios can be kicked off manually by name:
- `GET /api/release-tests` — list all presets
- `POST /api/release-tests/{name}/start` — start a preset (e.g. `retry-message-group`, `ingestion-load`)

## Layout

```
tools/testing-tool/
  TestingTool.slnx
  Dockerfile                     # multi-stage container build (build context = repo root)
  TestingTool/                   # ASP.NET Core host — API endpoints, web UI, NServiceBus endpoint,
                                 #   scenario runner, bypass writer, recoverability/search jobs,
                                 #   OTel setup, ServiceControl REST client
  TestingTool.Scenarios/         # IScenario contract + the five scenario implementations
  TestingTool.Contracts/         # shared DTOs (scenarios, status, jobs, bypass)
  TestingTool.SmokeTests/        # NUnit smoke tests (require a running ServiceControl + tool)
  TestingTool.AppHost/           # Aspire AppHost — orchestrates platform, tool, and observability stack
    obs/                         # observability config: OTel Collector, Prometheus, Grafana provisioning
                                 #   + prebuilt dashboard
```

## Run locally

```bash
dotnet build tools/testing-tool/TestingTool.slnx --configuration Release
dotnet run --project tools/testing-tool/TestingTool --configuration Release
```

Open http://localhost:5290 (or the port shown in the console).

## Run in a container

The tool ships a multi-stage Dockerfile that uses the same chiseled base image as the ServiceControl
containers. The build context is the repository root (so `global.json` and `nuget.config` are
available):

```bash
docker build -f tools/testing-tool/Dockerfile -t particular/testing-tool .
docker run --rm -p 8080:8080 \
  -e TestingTool__ServiceControlApiUrl=http://host.docker.internal:33333 \
  particular/testing-tool
```

The container listens on port 8080. CI ([testing-tool-ci.yml](/.github/workflows/testing-tool-ci.yml))
builds the solution and the container image on every change under `tools/testing-tool/`.

## Run with Aspire

The Aspire AppHost orchestrates the testing tool together with the full Particular platform
(ServiceControl + Learning transport + RavenDB + ServicePulse) and a complete observability
stack (OTel Collector, Jaeger, Prometheus, Grafana), so a single command brings up the whole
system locally:

```bash
aspire run tools/testing-tool/TestingTool.AppHost/TestingTool.AppHost.csproj
```

To test a specific ServiceControl image tag (e.g. a PR-based prerelease tag):

```bash
aspire run tools/testing-tool/TestingTool.AppHost/TestingTool.AppHost.csproj -- --tag pr-1234
```

To select a persistence backend for the ServiceControl error instance (`RavenDb`,
`SqlServer`, or `PostgreSql`; defaults to `RavenDb`):

```bash
aspire run tools/testing-tool/TestingTool.AppHost/TestingTool.AppHost.csproj -- --persistence:RavenDb
```

`--persistence RavenDb` (space separator) is accepted too. Both flags may be combined:

```bash
aspire run tools/testing-tool/TestingTool.AppHost/TestingTool.AppHost.csproj -- --tag pr-1234 --persistence:SqlServer
```

### AppHost CLI options

All flags are passed after `--` to the AppHost. Each accepts either `--name value` (space
separator) or `--name:value` (colon separator):

| Flag | Default | Values | Description |
|---|---|---|---|
| `--persistence` | `RavenDb` | `RavenDb`, `SqlServer`, `PostgreSql` | Persistence backend for the ServiceControl error instance |
| `--tag` | *(none — uses the current build's image)* | any image tag, e.g. `pr-1234` or `6.3.1` | Override the ServiceControl container image tag (useful for testing PR-based prereleases) |
| `--error-ingestion-scale-unit` | `0` | non-negative integer | Number of additional error-ingestion-only scale-out instances to spin up alongside the primary error instance (each runs with `--error-ingestion-only`) |

The Aspire dashboard provides allocated ports for each service. The testing tool automatically
connects to ServiceControl via the platform's transport and REST API URL, and sends its OTLP
telemetry to the OTel Collector, which fans out traces to Jaeger and metrics to Prometheus.
Grafana (auto-provisioned with Prometheus + Jaeger data sources) provides a prebuilt dashboard
at the allocated port — log in with `admin`/`admin` or browse anonymously as Viewer.

### Observability stack

| Service | Image | Purpose |
|---|---|---|
| OTel Collector | `otel/opentelemetry-collector-contrib` | Receives OTLP, fans out traces → Jaeger, metrics → Prometheus exporter |
| Jaeger | `jaegertracing/all-in-one` | Distributed-trace UI — purpose-built trace analysis richer than the Aspire dashboard |
| Prometheus | `prom/prometheus` | Scrapes the collector's metrics exporter |
| Grafana | `grafana/grafana-oss` | Dashboards with auto-provisioned Prometheus + Jaeger data sources |

The stack is wired via `AddObservabilityStack()` in `ObservabilityExtensions.cs` so `AppHost.cs`
stays clean. Config files live under `obs/` next to the AppHost project. The prebuilt Grafana
dashboard ("Testing Tool — Error Load & Observability") shows errors/sec by scenario (handler
and bypass paths emitted separately and combined into the raised total), search latency p95,
replay/archive rates, and — using ServiceControl's own OTel ingestion metrics
(`sc.error.ingestion.*`) — side-by-side comparison of errors raised vs errors ingested (rate
and cumulative), ingestion duration p95, and ingestion outcome by result.

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

The tool is **stateless** — all state is in-memory per replica. Run a single instance via
`dotnet run` or the Aspire AppHost; for multi-replica deployments, bring your own orchestration
and give each replica a distinct shard id so deterministic failure decisions don't overlap:

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
| `TestingTool__ReplayInterval` | `00:02:00` | Default interval for the retry job |
| `TestingTool__ReplayMinGroupSize` | `1` | Min messages in a group before retrying |
| `TestingTool__SearchInterval` | `00:01:00` | Default interval for the search job |
| `TestingTool__ArchiveInterval` | `00:02:00` | Default interval for the archive job |
| `TestingTool__ArchiveMinGroupSize` | `1` | Min messages in a group before archiving |
| `TestingTool__RetentionSweepInterval` | `00:05:00` | Default interval for the retention-sweep job |
| `TestingTool__ServiceControlInputQueue` | `Particular.ServiceControl` | ServiceControl error instance input queue (custom-check reports destination) |
| `TestingTool__CustomCheckInterval` | `00:00:30` | Default interval for the custom-check-failures job |
| `TestingTool__CustomCheckHost` | `ServiceControl` | `Host` field on injected custom-check reports |
| `TestingTool__CustomCheckFailureProbability` | `0.4` | Probability (0–1) a given check is reported failed each cycle |
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

## Background

The tool was designed with reference to two prior load-generation tools:

- [ServiceControlFeeder](https://github.com/dvdstelt/ServiceControlFeeder) — writes raw failed-message envelopes straight to the ServiceControl error queue; the pattern behind the direct bypass writer
- [FakeMessageGen](https://github.com/ramonsmits/FakeMessageGen) — high-throughput, rate-controlled fake message generation
