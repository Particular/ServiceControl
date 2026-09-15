# Migrating from RavenDB to SQL Server or PostgreSQL

This page covers what you can run today. How the migration works, and what is planned, is in the [migration overview](ravendb-to-sql-migration-overview.md) and the [system design diagram](migration-system-design-diagram.png).

> [!NOTE]
> Copying data is not built yet. The one migration command available is the source report, which reads the RavenDB database and changes nothing.

## Before you start

The source is a ServiceControl error instance on RavenDB. Keep its RavenDB settings in its configuration: the migration reads RavenDB through them, including after `PersistenceType` is switched to SQL Server or PostgreSQL.

| Setting | Environment variable | What it is |
| --- | --- | --- |
| `ServiceControl/RavenDB/ConnectionString` | `SERVICECONTROL_RAVENDB_CONNECTIONSTRING` | An external RavenDB server. Leave unset for an embedded database |
| `ServiceControl/DbPath` | `SERVICECONTROL_DBPATH` | The embedded database's data directory |
| `ServiceControl/RavenDB/DatabaseName` | `SERVICECONTROL_RAVENDB_DATABASENAME` | The primary database, `primary` by default |
| `LicensingComponent/RavenDB/ThroughputDatabaseName` | `LICENSINGCOMPONENT_RAVENDB_THROUGHPUTDATABASENAME` | The throughput database, `throughput` by default |
| `ServiceControl/RavenDB/ClientCertificatePath` or `ServiceControl/RavenDB/ClientCertificateBase64`, with `ServiceControl/RavenDB/ClientCertificatePassword` | `SERVICECONTROL_RAVENDB_CLIENTCERTIFICATEPATH` and so on | A secured external server's client certificate |
| `ServiceControl/ErrorRetentionPeriod` | `SERVICECONTROL_ERRORRETENTIONPERIOD` | Required. Don't change it during the move |

`ServiceControl/Migration/SourcePersistenceType` defaults to `RavenDB` and needs no setting.

## Report on the source

Run the instance's executable with `--migration-source-report`:

```powershell
# Installed on Windows, from the instance's installation folder
.\ServiceControl.exe --migration-source-report
```

```shell
# Container, against an external RavenDB server
docker run --rm --env-file servicecontrol.env ghcr.io/particular/servicecontrol:<version> --migration-source-report
```

From source, build `src/ServiceControl` and run the same command from its output folder, as in [How to run/debug locally](../../README.md#how-to-rundebug-locally).

The report prints the RavenDB server version, whether the source is embedded or external and where it is, both database names with the setting each came from, and a row count for every collection in both databases.

- **External server:** run it while ServiceControl is running. It only reads.
- **Embedded database:** stop the ServiceControl service, run the report, then start the service again. The report starts its own RavenDB process against the data directory, which cannot happen while the instance holds it.
- **Container with an embedded database:** not supported, because the container image does not ship the RavenDB server. Point the instance at an external RavenDB server instead.

## If the report fails

The error names the setting to fix:

- **"has no database named ..."**: the database name setting it quotes is wrong.
- **"refused its client certificate access ..."**: grant that certificate Read access to the database, or supply a certificate that has it.

## Not available yet

Copying the data (`MigrationMode`), the dry run, and the status and verify commands are planned but not built. The planned steps are in [Migration workflow](ravendb-to-sql-migration-overview.md#migration-workflow).
