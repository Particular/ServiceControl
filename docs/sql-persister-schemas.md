# SQL persister schemas

The SQL Server and PostgreSQL persisters keep all of an instance's tables in one schema, set by `Database/Schema`. A blank or absent setting selects a default:

| Instance | Default schema |
|---|---|
| Primary (error) | `sc` |
| Audit | `audit` |

* ServiceControl will attempt to create the configured schema if it does not already exist. If using a connection without this permission it should be created in advance with the appropriate rights granted to the connection user.
* A primary and an audit instance can share one schema, but sharing is not recommended.
* The primary instance's usage report lists `Storage.Schema` as `Default` for `sc` and `Custom` for other configured schemas. See [Usage report contents](usage-report.md).

ServiceControl does not rename or move existing schemas or tables. An instance that already holds data must set `Database/Schema` to the schema that contains it, such as `dbo` on SQL Server, `public` on PostgreSQL, or a custom schema. Otherwise, the instance uses the default schema instead of the schema containing its existing data.

The primary instance records applied migrations in the `__ErrorMigrationsHistory` table of its schema, as the audit instance does with `__AuditMigrationsHistory`. An existing primary database created with the default `__EFMigrationsHistory` table must have that table renamed to `__ErrorMigrationsHistory` before the instance starts. Otherwise, setup runs the migrations again and fails on the existing tables.
