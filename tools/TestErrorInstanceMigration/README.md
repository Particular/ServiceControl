# Replacing a ServiceControl Error instance with Aspire

This Aspire solution exercises the [Error instance replacement process](https://docs.particular.net/servicecontrol/migrations/replacing-error-instances/).

## Prerequisites

- .NET 10 SDK
- Aspire CLI
- A container runtime
- An Azure Service Bus connection string (supplied via the `asb-connection-string` parameter)
- A Particular Platform license in a standard license location or `PARTICULARSOFTWARE_LICENSE`

If `--tag` is omitted, published `latest` ServiceControl images are used. A non-`latest` tag selects the corresponding `ghcr.io/particular/servicecontrol*` CI images, which is useful when testing the SQL Server and PostgreSQL persisters from a branch build.

## Run the migration stages

Run commands from this directory and keep `--persistence` and `--tag` unchanged between the last two stages.

```bash
aspire run --project AppHost/AppHost.csproj -- \
  --mode PreMigration \
  --persistence SqlServer
```

Open the `deterministic-failing-endpoint` URL in the Aspire dashboard and generate failures:

```bash
curl -X POST 'http://localhost:<endpoint-port>/errors?count=10'
```

The payloads and IDs are random, but their failure is deterministic: the handler always throws, including after a ServicePulse retry.

Next, restart in side-by-side mode:

```bash
aspire run --project AppHost/AppHost.csproj -- \
  --mode SideBySide \
  --persistence SqlServer
```

The old RavenDB Error instance remains available through ServicePulse but no longer ingests from the error queue. Retry or archive its remaining failures. Retried messages fail again and are ingested by `new-error`.

Finally, restart without the old Error instance:

```bash
aspire run --project AppHost/AppHost.csproj -- \
  --mode PostMigrationMode \
  --persistence SqlServer
```

ServicePulse now points to `new-error`. The shared RavenDB-backed Audit instance remains registered as its remote instance.

Use `PostgreSql` (or `Postgres`) instead of `SqlServer` to test PostgreSQL. `Sql` is also accepted as an alias for `SqlServer`.

## Persistent state

Named volumes preserve:

- the original RavenDB Error and shared Audit data;
- SQL Server or PostgreSQL target data; and
- target Error message bodies.

The transport is Azure Service Bus. The connection string is supplied as a secret Aspire parameter (`asb-connection-string`), which can be set via user secrets, environment variable, or the Aspire run prompt.
