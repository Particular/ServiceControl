# SQL persister schemas

The SQL Server and PostgreSQL persisters keep all of an instance's tables in one schema, set by `Database/Schema`. A blank or absent setting selects a default:

| Instance | Default schema |
|---|---|
| Primary (error) | `psw_sc` |
| Audit | `psw_audit` |

* ServiceControl does not create the schema. Create it, and grant the configured user rights on it, before running setup.
* A primary and an audit instance cannot share a schema. Setup fails before it applies any migration when the other instance's migrations history table is in the target schema: the primary rejects `__AuditMigrationsHistory`, and the audit instance rejects `__EFMigrationsHistory`.
* The primary instance's usage report lists `Storage.Schema` as `Default` for `psw_sc` and `Custom` for other configured schemas. See [Usage report contents](usage-report.md).
