# SQL persister schemas

The SQL Server and PostgreSQL persisters keep all of an instance's tables in one schema, set by `Database/Schema`. A blank or absent setting selects a default:

| Instance | Default schema |
|---|---|
| Primary (error) | `sc_primary` |
| Audit | `sc_audit` |

* ServiceControl does not create the schema. Create it, and grant the configured user rights on it, before running setup.
* A primary and an audit instance cannot share a schema. Setup fails before it applies any migration when the other instance's migrations history table is in the target schema: the primary rejects `__AuditMigrationsHistory`, and the audit instance rejects `__EFMigrationsHistory`.
* The primary instance's usage report lists `Storage.Schema` as `Default` for `sc_primary` and `Custom` for other configured schemas. See [Usage report contents](usage-report.md).

ServiceControl does not rename or move existing schemas or tables. An instance that already holds data must set `Database/Schema` to the schema that contains it, such as `dbo` on SQL Server, `public` on PostgreSQL, or a custom schema. Otherwise, the instance uses the default schema instead of the schema containing its existing data.

The primary instance records applied migrations in the `__ErrorMigrationsHistory` table of its schema, as the audit instance does with `__AuditMigrationsHistory`.
