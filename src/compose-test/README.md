# Docker Compose integration test

The files in this directory are part of a test meant to test that all containers will run successfully for each transport. This is meant to detect issues that may stem from incompatibility between the application code, base image, and message transport. The test only ensures that the containers run and report as healthy within an allowable startup time. No further ServiceControl functionality is verified.

## Test components

* [Test workflow](/.github/workflows/container-integration-test.yml), which is referenced by the [CI workflow](/.github/workflows/container-integration-test.yml)
* compose.yml in this directory - The main Docker Compose file containing a service definition for each ServiceControl instance as well as a database instance
* Transport-specific Docker Compose files that extend compose.yml with a transport-specific container:
  * rabbit.yml
  * mssql.yml
  * sqs.yml - Uses [LocalStack](https://www.localstack.cloud/) instead of connecting to actual AWS services


