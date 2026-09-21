# Migrate from RavenDB to SQL Server or PostgreSQL

This page tells you what you can do today. [The migration overview](ravendb-to-sql-migration-overview.md) tells you how the migration will work. [How the migration is put together](ravendb-to-sql-migration-system-design.md) tells you which class does what.

> [!NOTE]
> You cannot copy data yet. This build has one migration command, the source report. If you set `ServiceControl/Migration/Enabled` to `true`, ServiceControl does not start. [What is not built yet](#what-is-not-built-yet-and-what-happens-if-you-turn-it-on) tells you why.
>
> The report opens RavenDB read-only, and the client refuses every write. But RavenDB deletes documents that are past their retention date, and this deletion starts when RavenDB loads a database. If you keep the RavenDB database as a fallback, make a backup of it before you start the report.

## Before you start

The source is a ServiceControl error instance on RavenDB. Keep the RavenDB settings in the configuration of that instance. The migration reads RavenDB through these settings. This is also true after you change `ServiceControl/PersistenceType` to SQL Server or PostgreSQL.

| Setting | Environment variable | What it is |
| --- | --- | --- |
| `ServiceControl/RavenDB/ConnectionString` | `SERVICECONTROL_RAVENDB_CONNECTIONSTRING` | The address of an external RavenDB server. Leave it unset for an embedded database |
| `ServiceControl/DbPath` | `SERVICECONTROL_DBPATH` | The data directory of the embedded database |
| `ServiceControl/RavenDB/DatabaseName` | `SERVICECONTROL_RAVENDB_DATABASENAME` | The primary database. The default is `primary` |
| `LicensingComponent/RavenDB/ThroughputDatabaseName` | `LICENSINGCOMPONENT_RAVENDB_THROUGHPUTDATABASENAME` | The throughput database. The default is `throughput` |
| `ServiceControl/RavenDB/ClientCertificatePath` or `ServiceControl/RavenDB/ClientCertificateBase64`, with `ServiceControl/RavenDB/ClientCertificatePassword` | `SERVICECONTROL_RAVENDB_CLIENTCERTIFICATEPATH` and so on | The client certificate for a secured external server |
| `ServiceControl/ErrorRetentionPeriod` | `SERVICECONTROL_ERRORRETENTIONPERIOD` | Required. ServiceControl does not start without it |

`ServiceControl/Migration/SourcePersistenceType` has the default `RavenDB`. You do not have to set it.

In an environment variable you can omit the `SERVICECONTROL_` prefix. `ServiceControl` is one of three namespaces that permit the short form. You cannot omit the `LICENSINGCOMPONENT_` prefix.

A SQL Server target must have Full-Text Search installed. Message search needs it, so `--setup` fails without it. A stock SQL Server container image does not have it. PostgreSQL needs nothing extra.

Start `ServiceControl.exe` with the `--setup` argument on every instance that already uses SQL Server or PostgreSQL. Do this when you upgrade to this build, and do it even if you do not intend to migrate. `--setup` adds the `MigrationCheckpoints` table. Every SQL instance reads this table one time at startup, to make sure that the table is there. An instance that you upgrade without `--setup` does not have the table, and it does not start.

Not built yet: when the copy is available, you must not change `ServiceControl/ErrorRetentionPeriod` during the move. The copier will use it to decide which rows are past the retention period.

## Make the source report

Start the instance executable with the `--migration-source-report` argument.

```powershell
# Windows installation. Start this from the installation folder of the instance.
.\ServiceControl.exe --migration-source-report
```

```shell
# Container, against an external RavenDB server
docker run --rm --env-file servicecontrol.env ghcr.io/particular/servicecontrol:<version> --migration-source-report
```

To do this from source, build `src/ServiceControl`. Then start the same command from its output folder. [How to run/debug locally](../../README.md#how-to-rundebug-locally) gives the steps.

The report prints:

- The source persistence that it read.
- The RavenDB server version.
- Whether the source is embedded or external, and where it is.
- The name of each of the two databases, and the setting that each name came from.
- A row count for every collection in both databases.

The report exits with 0 after it prints a report. It exits with 1 if it cannot print one. A script can thus tell the two apart without a read of the output.

Where you start the report depends on the source:

- External server: start the report while ServiceControl runs. The report sends only reads. The expiration warning at the top of this page applies to a fallback server.
- Embedded database: stop the ServiceControl service first. Start the report, then start the service again. The report starts its own RavenDB process against the data directory. This is not possible while the instance holds that directory.
- Container with an embedded database: not supported. The container image does not contain the RavenDB server. Point the instance at an external RavenDB server instead.

## If the report fails

The error message tells you what to correct.

- `has no database named ...`: the database name in the setting that the message quotes is wrong. Correct that setting.
- `refused its client certificate access ...`: give that certificate Read access to the database. Or supply a certificate that already has this access.
- `is secured but no client certificate is configured`: the connection string starts with `https://`. Set `ServiceControl/RavenDB/ClientCertificatePath` or `ServiceControl/RavenDB/ClientCertificateBase64`.
- `is valid from ... to ..., which does not include now`: the client certificate expired, or it is not yet valid. Supply a current certificate. The report does this check before the first request. Without the check, an expired certificate gives the same error as a refused connection.
- `ServiceControl expects RavenDB Server version ... or higher`: the external server is older than the RavenDB client in this build. Upgrade the server. The report does not do this check on an embedded source, because that server is installed with the client.
- `could not start a server for the embedded database ...`: the remainder of that line gives the reason from the server. The usual reason is a ServiceControl instance that still holds the data directory. Stop that instance, then start the report again. The other two reasons are a port already in use, and a missing RavenDB server.
- `did not finish loading within 5 minutes`: another process holds the embedded data directory, or the directory is corrupt. The wait is fixed at 5 minutes and no setting changes it.

## What is not built yet, and what happens if you turn it on

This build does not have the copy, the dry run, the status command or the verify command. [Migration workflow](ravendb-to-sql-migration-overview.md#migration-workflow) gives the planned steps.

If you set `ServiceControl/Migration/Enabled` to `true`, the migration startup checks run before ServiceControl opens. One check always fails in this build. The host does not start, nothing is copied, and nothing is opened on the target. Leave the setting at its default of `false`.

Which check fails depends on `ServiceControl/PersistenceType`:

- `RavenDB`: the first check refuses the pair, with the message `Migrating from 'RavenDB' to 'RavenDB' is not supported`.
- SQL Server or PostgreSQL: the check that asks whether this build can copy every required category fails. This build can copy 2 of the 12 required categories, and the message names the 10 that it cannot copy.

Five more settings sit beside `ServiceControl/Migration/Enabled`, and none of them does anything in this build: `ServiceControl/Migration/OptionalCategories`, `ServiceControl/Migration/ThrottlePauseMilliseconds`, `ServiceControl/Migration/HaltThresholdPercent`, `ServiceControl/Migration/HaltThresholdMinimum` and `ServiceControl/Migration/AllowIncompleteExit`. The code reads the first four only after the check that always fails. Nothing reads the fifth. These settings will be used in later builds.
