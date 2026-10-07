# Migration Engine: Instructions

> [!NOTE]
> This build does not copy data yet. Only `--migration-source-report` works, and ServiceControl does not start if `ServiceControl/Migration/Enabled` is `true`.

This page tells you how to move the data of a ServiceControl error instance from RavenDB to SQL Server or PostgreSQL. ServiceControl copies the data itself, so you do not need a separate tool. [The migration overview](ravendb-to-sql-migration-overview.md) explains how the migration works and why.

The migration does not move the audit instance or the monitoring instance. The audit instance has no migration path and the monitoring instance keeps its data in memory, so it has nothing to move.

## How the migration runs

ServiceControl copies the data it needs to open while it is closed. **This part is your outage**. Then it opens and copies the event log and the archived and resolved failed messages in the background. **The migration never writes to RavenDB**.

> [!IMPORTANT]
> Until ServiceControl opens, you can go back to RavenDB and lose nothing. After it opens, you cannot go back.

The migration copies the data in categories, and each category is Copying, Done, Failed or Abandoned. [The overview](ravendb-to-sql-migration-overview.md#when-something-goes-wrong-you-decide-what-happens-next) explains the categories and the states.

## Before you start

Make sure that your setup is supported:

- The RavenDB source is one of these:
  - embedded on the host (Windows installation only)
  - self-hosted on your network, or RavenDB Cloud
- The primary database and the throughput database of RavenDB are on the same server or cluster.
- The SQL target is SQL Server or PostgreSQL.
- A SQL Server target has Full-Text Search installed. Message search needs it, so `--setup` fails without it. The stock SQL Server container image does not have it. PostgreSQL needs nothing extra.
- The ServiceControl host can connect to RavenDB, to the SQL database, and to the message body storage at the same time.
- If ServiceControl runs in a container, RavenDB is an external server. The container image does not contain the RavenDB server, so it cannot open an embedded database.

Get the data ready:

- Resolve or archive as many unresolved failed messages as you can. They never age out, and ServiceControl stays closed until they are all copied. **A large backlog makes the outage long**.
- Run `--import-failed-errors` while the instance is still on RavenDB. A failed error import is a message that ServiceControl took off the error queue but failed to store. The migration copies these either way, but imported ones arrive as ordinary failed messages.

Keep these settings as they are:

- Keep `ServiceControl/RetryHistoryDepth` above 0. If it is 0 or less, the migration refuses to start. At that value, the first completed retry after the move deletes all the copied retry history.
- Do not change `ServiceControl/ErrorRetentionPeriod` or `ServiceControl/EventRetentionPeriod` until the migration is finished. The migration uses them to decide which rows are too old to copy. If one changes after the copy starts, ServiceControl refuses to start.

## Keep the RavenDB settings

The migration reads RavenDB through the RavenDB settings that the instance already has. You do not add new settings for the source. Keep these settings in the configuration of the instance, also after you change `ServiceControl/PersistenceType` to SQL Server or PostgreSQL.

| Setting | Environment variable | What it is |
| --- | --- | --- |
| `ServiceControl/RavenDB/ConnectionString` | `SERVICECONTROL_RAVENDB_CONNECTIONSTRING` | The address of an external RavenDB server. Leave it unset for an embedded database. |
| `ServiceControl/DbPath` | `SERVICECONTROL_DBPATH` | The data directory of the embedded database. |
| `ServiceControl/RavenDB/DatabaseName` | `SERVICECONTROL_RAVENDB_DATABASENAME` | The primary database. The default is `primary`. |
| `LicensingComponent/RavenDB/ThroughputDatabaseName` | `LICENSINGCOMPONENT_RAVENDB_THROUGHPUTDATABASENAME` | The throughput database. The default is `throughput`. |
| `ServiceControl/RavenDB/ClientCertificatePath` or `ServiceControl/RavenDB/ClientCertificateBase64`, with `ServiceControl/RavenDB/ClientCertificatePassword` | `SERVICECONTROL_RAVENDB_CLIENTCERTIFICATEPATH` and so on | The client certificate for a secured external server. |
| `ServiceControl/ErrorRetentionPeriod` | `SERVICECONTROL_ERRORRETENTIONPERIOD` | Required. ServiceControl does not start without it. |

In an environment variable, you can leave out the `SERVICECONTROL_` prefix. You cannot leave out the `LICENSINGCOMPONENT_` prefix.

## Step 1: Upgrade ServiceControl and make the source report

> [!IMPORTANT]
>
> - Upgrade the instance to the latest version in the usual way, and keep it on RavenDB. The version that copies the data must be the version that last ran against RavenDB.
> - Do not start ServiceControl again until step 6. If ServiceControl starts on the new database without the migration turned on, it writes rows to the database, and the migration then refuses that database. If this happens, start again from a new, empty database.

Then make the source report. The report shows what ServiceControl can read from RavenDB, so you find a wrong setting early. Start the instance executable with the `--migration-source-report` argument:

```powershell
# Windows installation. Start this from the installation folder of the instance.
.\ServiceControl.exe --migration-source-report
```

```shell
# Container, against an external RavenDB server.
docker run --rm --env-file servicecontrol.env ghcr.io/particular/servicecontrol:<version> --migration-source-report
```

Where you run the report depends on the source:

- External server: run the report while ServiceControl runs. The report only reads.
- Embedded database: stop the ServiceControl service first. Run the report, then start the service again. The report starts its own RavenDB process on the data directory, and it cannot do that while the instance holds the directory.

The report prints:

- The source persistence that it read.
- The RavenDB server version.
- Whether the source is embedded or external, and where it is.
- The name of each of the two databases, and the setting that each name came from.
- A row count for every collection in both databases.

The report exits with 0 when it prints a report, and with 1 when it cannot. If the report fails, read [Errors from the RavenDB source](#errors-from-the-ravendb-source).

## Step 2: Create an empty SQL database

1. Create a new database on SQL Server or PostgreSQL.
2. In the configuration of the instance, set `ServiceControl/PersistenceType` to `SQLServer` or `PostgreSQL`.
3. Set `ServiceControl/Database/ConnectionString` to the new database.
4. Set `ServiceControl/MessageBody/StorageType` to `FileSystem`, `AzureBlob` or `S3`, and set the settings for that storage type.
5. Run `ServiceControl.exe --setup`. This creates the SQL schema, which includes the table where the migration saves its progress.

## Step 3: Turn the migration on

1. Set `ServiceControl/Migration/Enabled` to `true`.
2. If you want less optional data, set a shorter window for the optional categories. A window is how far back an optional category copies. Set it to `0` to turn the category off.

| Setting | Default | What it does |
| --- | --- | --- |
| `ServiceControl/Migration/EventLogWindow` | The value of `ServiceControl/EventRetentionPeriod` | How far back the event log copy goes. |
| `ServiceControl/Migration/ArchivedAndResolvedFailedMessagesWindow` | The value of `ServiceControl/ErrorRetentionPeriod` | How far back the copy of archived and resolved messages goes. |

By default, the migration copies all the optional data that RavenDB still holds (current retension period setting). A window longer than the retention period copies nothing extra. After a category starts copying, you cannot change its window. To stop it early, abandon it.

## Step 4: Run the dry run

The dry run reads RavenDB and tells you what the migration will do, including a range for how long ServiceControl will be closed. It does not write anything. Run it with the `--migration-dry-run` argument, in the same way as the source report. [The overview](ravendb-to-sql-migration-overview.md#the-dry-run) lists what it reports.

Book the outage from the range in the report. The range is the shortest time the outage can take, not a promise. If the report says that a required category will end Failed and that a retry cannot fix it, plan to abandon that category after it fails.

## Step 5: Back up RavenDB

> [!IMPORTANT]
> Back up both RavenDB databases, primary and throughput, before you start the copy. Or turn off RavenDB expiration on them.

RavenDB keeps deleting old failed messages and event log items during the migration and after it. Without a backup, the RavenDB database that you keep to go back to loses data every day.

## Step 6: Start ServiceControl

Start the ServiceControl service, or the container. ServiceControl then does these things in order:

1. It runs every startup check. If a check fails, ServiceControl does not start and names the check. It copies nothing.
2. It copies every required category while it is closed. If a required category fails, ServiceControl stays closed. It says which category failed, why, and which command to run.
3. It opens when every required category is Done or Abandoned. You do not need to restart it.
4. It copies the optional categories in the background.

> [!IMPORTANT]
> Do not start any `--error-ingestion-only` worker until ServiceControl opens. A worker refuses to start while a required category is unfinished, but a worker that starts at the same time as ServiceControl can start before the copy saves its first progress, and nothing stops it.

If something goes wrong, read [If something goes wrong](#if-something-goes-wrong).

## Step 7: Watch the background copy

ServiceControl is open and works as usual while the optional categories copy. You can see the progress in three places:

- The ServicePulse activity feed. It shows when a category fails and when RavenDB cannot be reached.
- The ServiceControl log.
- The output of `--migration-status`. This command does not open RavenDB, so you can run it at any time.

If the copy slows down your production work, raise `ServiceControl/Migration/ThrottlePauseMilliseconds` and restart. This is the pause between two background batches. The default is 100 milliseconds.

> [!IMPORTANT]
> Turning the migration off does not stop the copy, because ServiceControl refuses to start until every category is Done or Abandoned. See [how to abandon a category](#a-category-is-failed).

Keep RavenDB running until every category is Done or Abandoned. If RavenDB goes down after ServiceControl opens, ServiceControl keeps running. The optional categories wait for RavenDB to come back, or you can abandon them.

## Step 8: Finish the migration

1. Run `--migration-verify`. It shows the row counts in RavenDB and in SQL, and explains the skipped and merged rows. It exits with 0 only when every category is Done or Abandoned.
2. Set `ServiceControl/Migration/Enabled` to `false`.
3. Restart ServiceControl.

The counts on the two sides can be different when nothing is wrong. [The overview](ravendb-to-sql-migration-overview.md#the-dry-run) explains why.

If you turn the migration off before every category is Done or Abandoned, ServiceControl refuses to start. The refusal names each unfinished category and the ways to continue.

## If something goes wrong

### A startup check fails

ServiceControl does not start, and its log names the check that failed. Nothing is copied. Correct the cause, then start ServiceControl again. The dry run runs the same checks, so you can use it to test your correction.

### A category is Failed

A category becomes Failed when it cannot copy rows, or when an error stops it. The rows that it already copied stay in SQL. A restart does not change a Failed category. You must run one of two commands.

#### To retry a category

1. Stop ServiceControl.
2. Read why the category failed. The reason is in the startup refusal and in the output of `--migration-status`.
3. Correct the cause. It is usually outside the migration, for example the body storage cannot be reached, a certificate expired, a database is down, or a disk is full.
4. Run `--migration-retry <category>` to reset the migration progress for that category.
5. Start ServiceControl. It copies the category again from the start.

#### To abandon a category

1. Stop ServiceControl.
2. Run `--migration-abandon <category>`. When you abandon a category, the categories that depend on it are abandoned too.
3. Start ServiceControl.

> [!IMPORTANT]
> **Abandon is final**. The rows that the category already copied stay in SQL, and the rest are not copied. Abandon a category when a retry cannot fix it, or when the data is not worth the time. You cannot retry the event log, so you can only abandon it.

You can abandon a required category only after it starts copying. You can abandon an optional category at any time, for example to stop a large copy early.

### You want to go back to RavenDB

You can go back to RavenDB without losing data only while ServiceControl is still closed:

1. Set `ServiceControl/Migration/Enabled` to `false`.
2. Set `ServiceControl/PersistenceType` back to RavenDB.
3. Start ServiceControl.

You lose only the copy. To try the migration again later, start from step 2 with a new, empty SQL database.

> [!IMPORTANT]
> Do not use the old copy again. The migration skips the categories that it finished, and misses everything that RavenDB received after that.

After ServiceControl opens on SQL, you cannot go back. You can only finish the migration, or abandon what is left.

### Errors from the RavenDB source

The source report, the dry run and the migration show these errors when they cannot read RavenDB:

- `has no database named ...`: the database name in the setting that the message names is wrong. Correct that setting.
- `refused its client certificate access ...`: give that certificate Read access to the database, or use a certificate that already has this access.
- `is secured but no client certificate is configured`: the connection string starts with `https://`. Set `ServiceControl/RavenDB/ClientCertificatePath` or `ServiceControl/RavenDB/ClientCertificateBase64`.
- `is valid from ... to ..., which does not include now`: the client certificate expired, or it is not valid yet. Use a current certificate.
- `ServiceControl expects RavenDB Server version ... or higher`: the external RavenDB server is older than this version of ServiceControl supports. Upgrade the RavenDB server.
- `could not start a server for the embedded database ...`: the rest of the line gives the reason. Usually a ServiceControl instance still holds the data directory. Stop that instance and try again. The other reasons are a port that is already in use, and a missing RavenDB server.
- `did not finish loading within 5 minutes`: another process holds the embedded data directory, or the directory is damaged. You cannot change the 5 minutes.

## Commands and settings

On an embedded source, stop ServiceControl before you run a command that opens RavenDB. **Only one RavenDB process can use the data directory**. The overview lists [every command and when it can run](ravendb-to-sql-migration-overview.md#the-dry-run), [the retry and abandon commands](ravendb-to-sql-migration-overview.md#retry-and-abandon), and [every migration setting](ravendb-to-sql-migration-overview.md#monitoring-and-settings).

### Run a command in a container

In a container, run each command as a one-off container of the same image, against the same databases:

```shell
docker run --rm --env-file servicecontrol.env ghcr.io/particular/servicecontrol:<version> --migration-status
```

The RavenDB source must be an external server.
