# Local Testing of Persistence Providers

ServiceControl supports multiple persistence types

* RavenDB (default)
* Microsoft SQL Server
* PostgreSQL

All persistence test projects can be run with `dotnet test` against the corresponding test project in `src/`.

## Test isolation on the SQL persisters

The SQL Server and PostgreSQL suites give each test its own **schema** in one shared database, named `sc_test_<guid>` for persistence tests and `sc_at_<guid>` for acceptance tests, and drop it on teardown. This uses the same `Database/Schema` setting that is offered to customers, so every run exercises that feature.

Two consequences are worth knowing:

* The connection string is used exactly as it is given, so **the database it names must already exist**. The test containers create one; a server you point the environment variable at will not.
* A test failure that leaves a process behind can leave a schema behind with it. `SELECT nspname FROM pg_namespace WHERE nspname LIKE 'sc\_%'` and `SELECT name FROM sys.schemas WHERE name LIKE 'sc[_]%'` will find any strays.

## RavenDB

RavenDB persistence tests start an embedded RavenDB instance for the duration of the test run.

## SQL Server

SQL Server persistence tests use [Testcontainers](https://testcontainers.com/) and expect the local image
`particular/servicecontrol-testing-sqlserver:latest`.

Build that image locally before running SQL Server persistence tests:

```shell
docker buildx build --platform=linux/amd64 --tag particular/servicecontrol-testing-sqlserver:latest ./src/Scripts/Docker/servicecontrol-testing-sqlserver
```

If you want to use an existing SQL Server instance instead of a test container, set the `ServiceControl_Persistence_SqlServer_ConnectionString` environment variable to a valid SQL Server connection string. It must name a database that exists, not `master`, because the tests create their schemas in whatever database it points at. The test container creates a `ServiceControlTests` database for this.

## PostgreSQL

PostgreSQL persistence tests use [Testcontainers](https://testcontainers.com/) and start a `postgres:16-alpine` container automatically.

If you want to use an existing PostgreSQL instance instead of a test container, set:

```shell
ServiceControl_Persistence_PostgreSql_ConnectionString
```

to a valid PostgreSQL connection string. It must name a database that exists, because the tests create their schemas in whatever database it points at. The test container creates a `servicecontroltests` database for this, so that test schemas do not end up in the `postgres` maintenance database.
