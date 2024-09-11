# Docker Compose integration test

The files in this directory are part of a test meant to test that all containers will run successfully for each transport. This is meant to detect issues that may stem from incompatibility between the application code, base image, and message transport. The test only ensures that the containers run and report as healthy within an allowable startup time. No further ServiceControl functionality is verified.

## Test components

* [Test workflow](/.github/workflows/container-integration-test.yml), which is referenced by the [CI workflow](/.github/workflows/container-integration-test.yml)
* `servicecontrol.yml` in this directory - The main Docker Compose file containing a service definition for each ServiceControl instance as well as a database instance. This can be used as-is with externalized transports (Azure Service Bus & Azure Storage Queues)
* Transport-specific Docker Compose files that extend compose.yml with a transport-specific container for message transports that support it:
  * rabbit.yml
  * mssql.yml
  * sqs.yml - Uses [LocalStack](https://www.localstack.cloud/) instead of connecting to actual AWS services
* `.env` file used by Docker Compose to provide environment variables that are used both by Docker Compose and by the containers it creates:
  * `SERVICECONTROL_TAG` - Used in the `servicecontrol.yml` file to determine what container tag to load from the GitHub Container Registry. For PRs this is going to be in the form of `pr-####` but for master/release branches this will be the MinVer version.
  * `TRANSPORTTYPE` - Proivdes the transport type name to all ServiceControl instances. Imports the value from the `matrix` from `env`.
  * `CONNECTIONSTRING` - Provides the transport connection string for all ServiceControl instances. If a value is present in the `matrix` (for Rabbit, MSSQL, SQS) then that is imported from `env`. The ASB/ASQ setup actions will overwrite the value for those transports.
  * `AWS_ENDPOINT_URL` - This is required since our AmazonSQS transport doesn't currently offer a way to set the endpoint URL to point to LocalStack via the connection string. The value is also used for other transports but has no effect there. Perhaps one day this can be moved to the connection string and removed from `.env`.

## How it works

### `matrix` element

The `matrix` element defines the values needed for each test case, one for each transport:

* `name` - A short name for the transport, is used for the name of the job, and to determine whether to run setup actions (like for ASB/ASQ) when a container is not available
* `transport` - ServiceControl's [TransportType](https://docs.particular.net/servicecontrol/transports). The `env` section adds this value to the environment for the whole job.
* `connection-string` - For container-based transports, this is known beforehand. The `env` section adds this value to the environment for the whole job. This is skipped for ASB/ASQ.
* `compose-cmd` - The Docker Compose command to run to set up the system. For container-based transports, multiple `-f` parameters are used to merge multiple compose files together. ASB/ASQ just use `servicecontrol.yml` alone.
* `expected-healthy-containers` - Determines how many containers must be `healthy` when doing `docker ps` to consider the test a success. This is 4 for ASB/ASQ (Primary, Audit, Monitoring, DB) with an additional 5th for tranpsorts that also have a transport container.

### Steps

* **Checkout** is necessary to clone the ServiceControl repo
* **Run MinVer** is necessary to calculate the version so that the correct containers can be loaded from ServiceControl's GitHub Container Registry
* **Azure Login**, **Setup Azure Service Bus**, and **Setup Azure Storage** are run only for the specific tests that require them. The `connection-string-name` parameter specifies that when the action completes, it should put the ASB/ASQ connection string into the `CONNECTIONSTRING` environment variable, which will be used by the `.env` file to provide the value to the containers.
* **Run Docker Compose** is where the system is spun up, using the `compose-cmd` value from the `matrix`.
* **Evaluate container health** waits for the containers to become healthy, breaking early if the `expected-healthy-containers` number is reached. It can take a bit for this to happen because GitHub Actions runners are not super powerful machines. A variant of `docker ps` with a filter for only `healthy` containers and JSON output is used to discover the healthy container count. At the end of this step, the healthy container count is written to GITHUB_OUTPUT so that it can be referenced in the last step.
* **Dump steps** dump potentially useful information to the output, so that in the event of a test failure, we can inspect what was going on in the containers. Multiple steps dump the output of `docker ps --all` to discover what containers exist and their status, plus the console logs of each of the instance containers.
* **Return status** grabs the number of healthy containers from the **Evaluate container health** step and exits with a non-zero exit code if it doesn't match the expected count so that the workflow will report failure.
