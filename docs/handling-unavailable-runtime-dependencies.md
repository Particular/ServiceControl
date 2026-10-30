# Unavailable runtime dependencies

ServiceControl instances (Main, Audit, and Monitoring) have runtime dependencies such as persistence and transport. They must handle the unavailability of these dependencies in a predictable way. Causes of unavailable dependencies include:

* invalid instance configuration, for example connection string or secrets
* network outages
* invalid network configuration, for example firewall misconfiguration
* data store failures, for example broken indexes or database process failures
* missing or invalid permissions
* uninitialized state, for example missing indexes or missing queues

These cases make some or all of the functionality of the platform unavailable.

## How ServiceControl handles unavailable runtime dependencies

ServiceControl instances handle unavailable runtime dependencies in one of two ways, depending on when they detect the failure:

* A failure detected during startup stops the instance immediately.
* A failure detected after a successful startup does not stop the instance. The instance is expected to try to recover once the dependency becomes available again.

### Startup checks

The Main and Audit instances run startup checks (`IStartupCheck`) in a hosted service that is registered before persistence, ingestion, and component hosted services. A failing check stops the instance before it ingests any message. The SQL Server and PostgreSQL persisters register these checks:

| ClassName | Prerequisites | Description |
| --- | --- | --- |
| `PendingMigrationsStartupCheck` | Main or Audit instance using SQL Server or PostgreSQL persistence | Checks that the database has no pending migrations. Runtime startup does not apply migrations; run `--setup` to apply them. |
| `FileSystemBodyStorageStartupCheck` | Main instance using SQL Server or PostgreSQL persistence with file system body storage | Checks that the configured body storage directory exists and is writable. Setup creates the directory but does not verify write access. |
