# Engineering context

This page points to sources that help explain ServiceControl's design, behavior, and testing practices.

## Start here

- [ServiceControl documentation](https://docs.particular.net/servicecontrol/) — public documentation entry point
- [README.md](../README.md) — how to run and debug ServiceControl, ServiceControl.Audit, and ServiceControl.Monitoring locally
- [Testing overview](testing.md) — index of test kinds and how to run each locally
- [Coding and design guidelines](coding-and-design-guidelines.md) — conventions for new code
- [Deployment](deployment.md) and [Packaging](packaging.md) — how instances are packaged and deployed

## Architecture and design

- [Ingestion pipeline](ingestion-pipeline.md) — why batch parallelism is a storage decision, not an instance decision
- [Error ingestion design](error-ingestion-design.md) — relational-persister error ingestion design
- [Bulk retries design](bulk-retries-design.md) — how ServiceControl retries failed messages in bulk
- [Retries over Azure Storage Queues transport](retries-asq-transport.md) — transport-specific retry handling
- [Data versioning design](data-versioning-design.md) — the cache-versioning invariant for API responses
- [Event log design](eventlog-design.md) — what the event log is and what it records
- [Multiple ServiceControl instances communication](multipleservicecontrolinstancescommunication.md) — how primary, audit, and monitoring instances talk to each other
- [Handling unavailable runtime dependencies](handling-unavailable-runtime-dependencies.md) — how instances react when a dependency is unavailable
- [Telemetry](telemetry.md) — telemetry configuration and emitted metrics
- [Throughput collection](throughput-collection.md) — why and how usage data is collected

## Testing guidance

- [Testing overview](testing.md)
- [Testing scenarios](testing-scenarios.md)
- [Local testing of persistence providers](testing-persistence.md)
- [Writing acceptance tests](writing-acceptance-tests.md)
- [Authentication testing](authentication-testing.md)
- [Forward headers testing](forward-headers-testing.md)
- [HTTPS testing](https-testing.md)
- [Reverse proxy testing](reverseproxy-testing.md)
- [Multi-version, multi-instance smoke test](multiversion-multiinstance-smoke-test.md)

## Decisions and rationale

- [Architecture and design decisions](decisions/)

### Decisions recorded in pull requests

A pull request is listed here only when it is the canonical record for a decision area: it establishes a durable constraint or convention, or rejects an alternative likely to return, and no `docs/` file or ADR covers it. Bug fixes and routine changes are not listed; recover them from `git log` and `gh pr view`.

- Pagination limits belong to the API layer, not the persister — [#5899](https://github.com/Particular/ServiceControl/pull/5899)
- Timestamps in the error instance come from an injected `TimeProvider`, not `DateTime.UtcNow` — [#5843](https://github.com/Particular/ServiceControl/pull/5843)
- HTTPS certificates load during settings validation so an unusable certificate fails fast, and an ingestion-only worker serves its own certificate — [#5891](https://github.com/Particular/ServiceControl/pull/5891), [#5923](https://github.com/Particular/ServiceControl/pull/5923)
- Endpoint throughput recording uses an atomic upsert because duplicate keys are routine, not exceptional — [#5895](https://github.com/Particular/ServiceControl/pull/5895)
- Failed audit imports use deterministic ids derived from the message id so repeated failures do not duplicate — [#5919](https://github.com/Particular/ServiceControl/pull/5919)
- SQL Server and PostgreSQL primaries can be upgraded, but the upgrade path does not create them — [#5921](https://github.com/Particular/ServiceControl/pull/5921)
- New RavenDB databases use the Lucene search engine, and indexes still on Corax are flagged — [#5833](https://github.com/Particular/ServiceControl/pull/5833)

Keep this index current when a canonical source is added, replaced, or retired; link, do not copy.
