# Testing

ServiceControl tests are designed to test different components and behaviors. This document outlines the tests in the repository and what they are meant to test.

## Unit tests

ServiceControl components have specific unit test projects verifying their behaviors and API.

## Packaging tests

Packaging tests check:

- Folder structure and content of the [packaging process](packaging.md) output.
- That packaged [assembly versions match](packaging.md#assembly-mismatches).

## Installation engine tests

Installation engine tests run partial installations and checks:

- That the generated configuration is correct.
- That transport and persistence are correctly extracted.

## Persistence tests

Persistence tests check assumptions at the persistence seam level by exercising each persister.

## Transport tests

Transport tests are done by executing the transport test suite for each transport.

## Acceptance tests

Run ServiceControl full version and use the HTTP API to validate results. LearningTransport is used for all tests.

## Multi-instance tests

Multi-instance tests validate the interaction between different ServiceControl instances. ServiceControl instances are run in-memory in the same process. LearningTransport is used for all tests.

## Container tests

Container images generated for all builds are pushed to the [GitHub container registry](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-container-registry). Once pushed, all images are tested by [spinning them all up for each supported transport](/src/container-integration-test/).

Containers built by a PR and stored on GitHub Container Registry can be tested locally:

1. [Authenticate to the GitHub Container Registry using a personal access token](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-container-registry#authenticating-with-a-personal-access-token-classic).
    - Create a [classic token](https://github.com/settings/tokens). Select the scope for `read:packages`
    - Copy the newly created token text.
    - **On Windows**
      - create an Environment Variable to store the token (e.g. GITHUB_TOKEN)
      - open powershell
      - run the command `echo $env:GITHUB_TOKEN | docker login ghcr.io -u <your particular email> --password-stdin`
    - ensure that you get a successful login message. use `docker logout ghcr.io` once the following steps are complete and consider removing the token from github if its no longer needed
2. In the terminal, navigate to `[/docs/test-ghcr-tag`](/docs/test/ghcr-tag).
3. Edit the [`.env` file](/docs/test-ghcr-tag/.env) to specify the PR-based tag (in the form `pr-####`) to test.
4. Run `docker compose up -d`.
5. Services will be available at the following URLs:
    * [RabbitMQ Management](http://localhost:15672) (Login: `guest`/`guest`)
    * [RavenDB](http://localhost:8080)
    * [ServiceControl API](http://localhost:33333/api)
    * [Audit API](http://localhost:44444/api)
    * [Monitoring API](http://localhost:33633)
    * [ServicePulse (latest from Docker Hub)](http://localhost:9090)
6. Tear down services using `docker compose down`.
