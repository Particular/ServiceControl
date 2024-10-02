# Testing

ServiceControl tests are designed to test difference components and behaviors. This document outlines the tests in the respository and what they are meant to test.

## Unit tests

ServiceControl compoonents have specific unit tests projects verifying their behaviors and API.

## Packaging tests

Packaging tests check:

- Folder structure and content of the [packaging process](packaging.md) output.
- That packaged [assembly versions match](packaging.md#assembly-mismatches).

## Installation engine tests

Installation engine tests run partial installations and checks:

- That the generated configuration is correct.
- That transport and persistence are correctly extracted.

## Persistence tests

Persistence tests check assumption at the persistence seam level by exercising each persister.

## Transport tests

Transport tests are done by executing the transport test suite for each transport.

## Acceptance tests

Run ServiceControl full version and use the HTTP API to validate results. LearningTransport is used for all tests.

## Multi-instance tests

Multi-instance tests validate the interaction between different ServiceControl instances. ServiceControl instances are run in-memory in the same process. LearningTransport is used for all tests.

## Container tests

Containers images generated for all builds are pushed to the [GitHub container registry](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-container-registry). Once pushed all images are tested by [spinning them all up for each supported transport](/src/container-integration-test/).
