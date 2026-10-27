# SQL persister schemas

The SQL Server and PostgreSQL persisters keep all of an instance's tables in one schema, set by `Database/Schema`. A blank or absent setting selects a default:

| Instance | Default schema |
|---|---|
| Primary (error) | None: the connection or provider default, such as `dbo` on SQL Server and `public` on PostgreSQL |
| Audit | `sc_audit` |

* During setup, ServiceControl attempts to create a missing schema, whether selected by default or configured through `Database/Schema`. If the configured database user lacks permission to create schemas, setup fails with a database permission error. In that case, create the schema and grant the user the required rights before running setup.
* A primary and an audit instance cannot share a schema within the same database. Instances in separate databases can use the same schema name. Setup checks the target schema in the connected database before applying migrations: the primary rejects `__AuditMigrationsHistory`, and the audit instance rejects `__EFMigrationsHistory`.
* The primary instance's usage report lists `Storage.Schema` as `Default` when no schema is configured and `Custom` for any configured schema, including `sc_primary`. See [Usage report contents](usage-report.md).

ServiceControl does not rename or move existing schemas or tables. An instance that already holds data in a named schema, such as `sc_primary` or a custom schema, must set `Database/Schema` to that schema. Otherwise, the instance uses the default schema instead of the schema containing its existing data.

The primary instance records applied migrations in the `__ErrorMigrationsHistory` table of its schema, as the audit instance does with `__AuditMigrationsHistory`.
