# Load testing

The [ServiceControl Testing Tool](../tools/testing-tool/README.md) is a stateless .NET service that generates error load and real-world failure scenarios against a test ServiceControl instance, to validate error-ingestion performance. It has a web UI for manual control, built-in failure scenarios (third-party outage, timeout spike, poison message, and more), and OpenTelemetry telemetry.

The [testing tool README](../tools/testing-tool/README.md) is the full reference: every scenario, API endpoint, configuration setting, container usage, and scaling notes.

## Quick start: full stack with Aspire

The Aspire AppHost starts the whole system with a single command: the ServiceControl platform (Learning transport), the testing tool, and a complete observability stack (OTel Collector, Jaeger, Prometheus, and Grafana with a prebuilt dashboard).

```bash
aspire run tools/testing-tool/TestingTool.AppHost/TestingTool.AppHost.csproj
```

Aspire assigns dynamic ports. Open the web UI of the testing tool through the link in the Aspire dashboard.

Useful flags, passed after `--`:

| Flag | Description |
|---|---|
| `--tag <tag>` | Test a specific ServiceControl image tag, for example a PR prerelease like `pr-1234` |
| `--persistence <type>` | Persistence for the error instance: `RavenDb` (default), `SqlServer`, or `PostgreSql` |
| `--error-ingestion-scale-unit <n>` | Start `n` additional error-ingestion-only ServiceControl instances |

## Run against an existing ServiceControl

```bash
dotnet build tools/testing-tool/TestingTool.slnx --configuration Release
dotnet run --project tools/testing-tool/TestingTool --configuration Release
```

Open http://localhost:5290. The tool defaults to a ServiceControl instance at `http://localhost:33333`; to use another instance, set the `TestingTool__ServiceControlApiUrl` environment variable.

## Generating load

Everything is controllable from the web UI, or via the HTTP API:

- Scenarios: the five built-in failure scenarios (`third-party-outage`, `timeout-spike`, `poison-message`, `deserialization-failure`, `background-noise`), each producing errors that group naturally. Start with `POST /api/scenarios/{name}/start` (optional `rate` and `durationSeconds`).
- Bypass writer: high-throughput load that writes failed-message envelopes directly to the ServiceControl error queue, skipping the message handler (`POST /api/bypass/start`).
- Jobs: retry, archive, search, retention sweep, and custom-check-failure jobs exercise ServiceControl's recoverability, FTS search, and retention pipelines against the load. Jobs do not start automatically. Start them from the UI or `/api/jobs`.
- Release-test presets: named presets mapped from the [testing scenarios](testing-scenarios.md), for example `POST /api/release-tests/ingestion-load/start`.

## Observing the run

- The Aspire dashboard shows logs and traces for every service.
- Grafana (auto-provisioned; log in with `admin`/`admin`) has a prebuilt "Testing Tool" dashboard with errors/sec by scenario, search latency p95, replay/archive rates, and errors raised versus ingested.
- Jaeger provides trace analysis and Prometheus provides metrics. The tool also exposes a Prometheus scraping endpoint at `/metrics`.
