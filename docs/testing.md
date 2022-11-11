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

Transport tests are currently performed by executing the entire test suite in CI for each supported transport.

## Acceptance tests

Run ServiceControl full version and use the HTTP API to validate results.

## Multi-instance tests

Multi-instance tests validate the interaction between different ServiceControl instances. ServiceControl instances are run in-memory in the same process.

## Docker tests

Are included in the multi-instance tests project, they validate text in the `dockerfile` files using approval tests.

TBD: delete tests, add some instructions on how to manually test docker images
