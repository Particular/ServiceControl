# Load Testing

The [ServiceControl Testing Tool](../tools/testing-tool/README.md) is a stateless .NET service that generates error load and real-world failure scenarios against a test ServiceControl instance, to validate error-ingestion performance. It ships with a web UI for manual control, built-in failure scenarios (third-party outage, timeout spike, poison message, and more), and OpenTelemetry telemetry.

This guide covers getting it up and running quickly. For the full reference — every scenario, API endpoint, configuration setting, container usage, and scaling notes — see the [testing tool README](../tools/testing-tool/README.md).

## Quick start: full stack with Aspire

The Aspire AppHost brings up the whole system in a single command: the ServiceControl platform (Learning transport), the testing tool, and a complete observability stack (OTel Collector, Jaeger, Prometheus, and Grafana with a prebuilt dashboard).

```bash
aspire run tools/testing-tool/TestingTool.AppHost/TestingTool.AppHost.csproj
```

Aspire assigns dynamic ports — open the testing tool's web UI via the link in the Aspire dashboard.

Useful flags, passed after `--`:

| Flag | Description |
|---|---|
| `--tag <tag>` | Test a specific ServiceControl image tag, e.g. a PR prerelease like `pr-1234` |
| `--persistence <type>` | Persistence for the error instance: `RavenDb` (default), `SqlServer`, or `PostgreSql` |
| `--error-ingestion-scale-unit <n>` | Spin up `n` additional error-ingestion-only ServiceControl instances |

## Run against an existing ServiceControl

```bash
dotnet build tools/testing-tool/TestingTool.slnx --configuration Release
dotnet run --project tools/testing-tool/TestingTool --configuration Release
```

Open http://localhost:5290. The tool defaults to a ServiceControl instance at `http://localhost:33333`; point it elsewhere with the `TestingTool__ServiceControlApiUrl` environment variable.

## Generating load

Everything is controllable from the web UI, or via the HTTP API:

- **Scenarios** — the five built-in failure scenarios (`third-party-outage`, `timeout-spike`, `poison-message`, `deserialization-failure`, `background-noise`), each producing naturally-grouped errors. Start with `POST /api/scenarios/{name}/start` (optional `rate` and `durationSeconds`).
- **Bypass writer** — high-throughput load that writes failed-message envelopes directly to the ServiceControl error queue, skipping the message handler (`POST /api/bypass/start`).
- **Jobs** — retry, archive, search, retention sweep, and custom-check-failure jobs exercise ServiceControl's recoverability, FTS search, and retention pipelines against the load. Jobs do not auto-start; kick them off from the UI or `/api/jobs`.
- **Release-test presets** — named presets mapped from the [testing scenarios](testing-scenarios.md), e.g. `POST /api/release-tests/ingestion-load/start`.

## Observing the run

- The Aspire dashboard shows logs and traces for every service.
- Grafana (auto-provisioned; log in with `admin`/`admin`) ships a prebuilt "Testing Tool" dashboard — errors/sec by scenario, search latency p95, replay/archive rates, and errors raised vs ingested.
- Jaeger for trace analysis, Prometheus for metrics. The tool also exposes a Prometheus scraping endpoint at `/metrics`.

## Smoke tests

With a stack running (in either mode above):

```bash
dotnet test tools/testing-tool/TestingTool.SmokeTests
```

Aspire ports are dynamic, so override the defaults to match your run:

```bash
TESTING_TOOL_URL=http://localhost:<tool-port> SERVICECONTROL_URL=http://localhost:<sc-port> \
  dotnet test tools/testing-tool/TestingTool.SmokeTests
```