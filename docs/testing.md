# Testing

ServiceControl tests cover different components and behaviors.

## Unit tests

ServiceControl components have specific unit test projects verifying their behaviors and API.

## Packaging tests

Packaging tests check:

- Folder structure and content of the [packaging process](packaging.md) output.
- That packaged [assembly versions match](packaging.md#assembly-mismatches).
- That `RavenDBServer.zip` holds the RavenDB 6.2 server pinned by `RavenDB6Version` and `RavenDB7Server.zip` holds the RavenDB 7.2 server pinned by `RavenDB.Embedded`.

## Installation engine tests

Installation engine tests run partial installations and check:

- That the generated configuration is correct.
- That transport and persistence are correctly extracted.
- That an instance receives the RavenDB server that matches its persister, on install and on upgrade.

On Windows, [PackagedRavenDBTests](../src/ServiceControlInstaller.Engine.UnitTests/Validation/PackagedRavenDBTests.cs) starts the server extracted from each of the installer's embedded `RavenDBServer.zip` (RavenDB 6.2) and `RavenDB7Server.zip` (RavenDB 7.2) archives, using a RavenDB 6.2 client. It verifies the native Kafka dependencies are absent and checks document persistence and indexing before and after a restart. These tests run in the `Windows-DefaultCore` CI job; the `Windows-Raven` tests use the separate server files supplied by `RavenDB.Embedded`. The tests of `RavenDBStorageSchema` and `RavenDBStorageUpgrade` read the storage versions of both bundled archives, and the instance tests check which server each persister receives.

## Persistence tests

Persistence tests check assumptions at the persistence seam level by exercising each persister. For local setup details, see [Local testing of persistence providers](testing-persistence.md). For the workflow that runs these suites against managed cloud databases, see [Cloud database tests](cloud-database-tests.md).

The RavenDB persistence test projects contain an `IndexDefinitionApprovals` test that approves the definition of every index the persister deploys. RavenDB rebuilds an index side by side when its definition changes, and the rebuild of a large database can take days. When the test fails, record the changed indexes in the upgrade guide of the next release before you approve the new definition. An operator who locked an index must unlock it to receive the change.

ServiceControl ships two RavenDB persisters: the main one on RavenDB 7.2 with the manifest name `RavenDB7`, and a legacy one on RavenDB 6.2 with the manifest name `RavenDB` and the alias `RavenDB6`. ServiceControl 8 removes the legacy persister. The main test projects are in the `Raven` test category and test the main persister. Four projects in the `Raven6` category test the legacy persister: `ServiceControl.Persistence.Tests.RavenDB6`, `ServiceControl.Audit.Persistence.Tests.RavenDB6`, `ServiceControl.AcceptanceTests.RavenDB6`, and `ServiceControl.Audit.AcceptanceTests.RavenDB6`.

The `RavenDB6` test projects own no tests. They link the source files of the matching main test project, define `RAVENDB6`, reference the legacy persister projects, and run the same tests against the RavenDB 6.2 client and server. When a test only applies to RavenDB 7.2, the `RavenDB6` project file excludes it with `Compile Remove` and a comment that gives the reason. When an expectation differs by flavor, the test reads it from production code, for example `RavenServerLogLevel.Default`. `RavenFlavour` holds the manifest name that each flavor of the tests selects. The multi-instance tests run on the main persister only.

Particular.Approvals reads approved files from the `ApprovalFiles` folder of the running test project, so each `RavenDB6` persistence test project keeps its own copy of the approved files. When a change updates an approved file in a main project, update the copy in the matching `RavenDB6` project. A copy that differs from the main file records behavior that is specific to RavenDB 6.2.

The SQL Server and PostgreSQL persistence test projects contain a `SlowMigrationOperationApprovals` test. It approves the migration steps that can block writes on a large table: index, primary key, and unique constraint changes, new foreign keys and check constraints, altered columns, raw SQL, and new columns that are NOT NULL with a default or are stored computed columns. Other steps, such as renames and drops of columns, tables, and constraints, are not listed. The test lists these steps for each migration and leaves out steps on a table that the same migration creates, because that table is empty. Raw SQL is always listed. When the test fails because a step changed, record the change in the upgrade guide of the next release before you approve the new list. If a step changed inside an existing migration, for example through a shared SQL constant, add a new migration instead. Databases that already ran the old migration never run it again.

## Transport tests

Transport tests run the transport test suite for each transport.

## Acceptance tests

Acceptance tests run the full ServiceControl and use the HTTP API to validate results. LearningTransport is used for all tests.

For how to write one that fails when it should, see [Writing acceptance tests](writing-acceptance-tests.md).

### Windows prerequisite: register the event sources

On Windows, every acceptance test fails on first run with:

```
System.Security.SecurityException : The source ServiceControl was not found on computer .,
but some or all event logs could not be searched.  Inaccessible logs: Security.
```

Setup registers a Windows event source, and checking whether one already exists enumerates every event log, including `Security`, which a process without administrator rights cannot read.

Register both sources once, from an **elevated** PowerShell prompt:

```powershell
[System.Diagnostics.EventLog]::CreateEventSource('ServiceControl', 'Application')
[System.Diagnostics.EventLog]::CreateEventSource('ServiceControl.Audit', 'Application')
```

Use the .NET API as shown rather than `New-EventLog`, which is not available in PowerShell 7. Afterwards the tests run normally without elevation, because the lookup finds the registered source before it needs to read `Security`.

## Multi-instance tests

Multi-instance tests validate the interaction between different ServiceControl instances. ServiceControl instances are run in-memory in the same process. LearningTransport is used for all tests.

## Container tests

Container images generated for all builds are pushed to the [GitHub container registry](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-container-registry). Once pushed, all images are tested by [starting them all for each supported transport](/src/container-integration-test/).

Containers built by a PR and stored on GitHub Container Registry can be tested locally:

1. [Authenticate to the GitHub Container Registry using a personal access token](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-container-registry#authenticating-with-a-personal-access-token-classic).
    - Create a [classic token](https://github.com/settings/tokens). Select the scope for `read:packages`
    - Copy the newly created token text.
    - Run the following command in a terminal:
       ```shell
       docker login ghcr.io
       ```
      Docker prompts for a username (your particular.net email) and a password (the token).
    - Confirm that the login succeeds.
    - Use `docker logout ghcr.io` once the following steps are complete. Consider removing the token from GitHub if you no longer need it.
2. In the terminal, go to [`/docs/test-ghcr-tag`](/docs/test/ghcr-tag).
3. Edit the [`.env` file](/docs/test-ghcr-tag/.env) to specify the PR-based tag (in the form `pr-####`) to test.
4. Run `docker compose up -d`.
5. Open the services at the following URLs:
    * [RabbitMQ Management](http://localhost:15672) (Login: `guest`/`guest`)
    * [RavenDB](http://localhost:8080)
    * [ServiceControl API](http://localhost:33333/api)
    * [Audit API](http://localhost:44444/api)
    * [Monitoring API](http://localhost:33633)
    * [ServicePulse (latest from Docker Hub)](http://localhost:9090)
6. Stop the services using `docker compose down`.

## Container tests using Aspire

The [Particular.Aspire.Hosting.ServicePlatform](https://github.com/Particular/Particular.Aspire.Hosting.ServicePlatform) package integrates the Particular Platform with the Aspire hosting platform. This package configures environment variables to attach the platform. A single-file apphost in [`test-ghcr-tag-aspire`](/docs/test-ghcr-tag-aspire) starts ServiceControl from a prerelease container image.

Containers built by a PR and stored on GitHub Container Registry can be tested locally:

1. Set up your GitHub Container Registry credentials as described in the [Container tests](#container-tests) section above.
2. Install the [Aspire CLI](https://aspire.dev/get-started/install-cli/).
3. Run `aspire update` so that the testing AppHost file `docs/test-ghcr-tag-aspire/AppHost.cs` uses the latest Aspire SDK and RabbitMQ integration package.
4. Run `aspire run docs/test-ghcr-tag-aspire/AppHost.cs -- tag` to start the application, where `tag` is the PR-based tag (in the form `pr-####`) to test. Without a tag, the command uses the `latest` tag.
5. Open the dashboard from the link in the terminal. The dashboard shows the assigned ports for each service:
    * RabbitMQ Management (Login: `guest`/`guest`)
    * RavenDB
    * ServiceControl API
    * Audit API
    * Monitoring API
    * ServicePulse (latest from Docker Hub)
6. Exit the CLI process. Aspire stops the application automatically.