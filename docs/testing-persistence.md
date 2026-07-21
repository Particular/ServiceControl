# Local Testing of Persistence Providers

ServiceControl supports multiple persistence types

* RavenDB (default)
* Microsoft SQL Server
* PostgreSQL

All persistence test projects can be run with `dotnet test` against the corresponding test project in `src/`.

## RavenDB

RavenDB persistence tests start an embedded RavenDB instance for the duration of the test run.

## SQL Server

SQL Server persistence tests use [Testcontainers](https://testcontainers.com/) and expect the local image
`particular/servicecontrol-testing-sqlserver:latest`.

Build that image locally before running SQL Server persistence tests:

```shell
docker buildx build --platform=linux/amd64 --tag particular/servicecontrol-testing-sqlserver:latest ./src/Scripts/Docker/servicecontrol-testing-sqlserver
```

If you want to use an existing SQL Server instance instead of a test container, set the `ServiceControl_Persistence_SqlServer_ConnectionString` environment variable to a valid SQL Server connection string.

## PostgreSQL

PostgreSQL persistence tests use [Testcontainers](https://testcontainers.com/) and start a `postgres:16-alpine` container automatically.

If you want to use an existing PostgreSQL instance instead of a test container, set:

```shell
ServiceControl_Persistence_PostgreSql_ConnectionString
```

to a valid PostgreSQL connection string.
