# Testing

ServiceControl tests cover different components and behaviors.

## Unit tests

ServiceControl components have specific unit test projects verifying their behaviors and API.

## Packaging tests

Packaging tests check:

- Folder structure and content of the [packaging process](packaging.md) output.
- That packaged [assembly versions match](packaging.md#assembly-mismatches).

## Installation engine tests

Installation engine tests run partial installations and check:

- That the generated configuration is correct.
- That transport and persistence are correctly extracted.

## Persistence tests

Persistence tests check assumptions at the persistence seam level by exercising each persister.
For local setup details, see [Local testing of persistence providers](testing-persistence.md).

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