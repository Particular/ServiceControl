# ServiceControl docker files

This folder contains all the Docker files required to build all ServiceControl container images for all the supported transports. For each transports there are 6 `.dockerfile` files. One for the main ServiceControl instance, one for ServiceControl.Audit instances, and one for ServiceControl.Monitoring instances. Each one has a corresponding `*.init-windows.dockerfile` to initialize the environment.

To build all the container images at once, run the `builddockerimages.ps1` PowerShell script.

NOTE: The script is provided to ease development stages only. To run container images in production refer to the ones available on Docker Hub.

## Building & staging docker images

The docker files are all built by GitHub Actions as part of the [release workflow](/.github/workflows/release.yml).

The workflow runs through each transport and builds 6 docker files for each transport using the artifacts from the initial build step. Once a docker image has been built it is pushed to dockerhub tagged as the specific version that has been built.

E.g. If we were deploying version 4.15.0 of ServiceControl, the deploy step will build the following 6 containers for SqlServer and tag them `4.15.0`:

* `particular/servicecontrol.sql-windows:4.15.0`
* `particular/servicecontrol.sql.init-windows:4.15.0`
* `particular/servicecontrol.sql.audit-windows:4.15.0`
* `particular/servicecontrol.sql.audit.init-windows:4.15.0`
* `particular/servicecontrol.sql.monitoring-windows:4.15.0`
* `particular/servicecontrol.sql.monitoring.init-windows:4.15.0`

These images are tagged with the specific version of ServiceControl being built and pushed to the corresponding public `particular/servicecontrol.{specific}` repositories. At this point, the docker images are considered staged. This means that if someone is watching the feed directly, they can install the staged images by explicitly specifying the exact tag, e.g. `docker pull particular/servicecontrol.sql-windows:4.15.0`.

Each container is built as a separate job so that the building can happen in parallel.

Note: Linux containers have to be built on Linux agents, and Windows containers have to be built on Windows agents. Only Windows contains have the suffix `-windows`. Linux containers will not have this suffix.

## Promoting docker images to production

When a ServiceControl release is promoted to production, one of the steps is to take the staged images and to re-tag them as the following:

* `particular/servicecontrol.sql-windows:4.15.0` => `particular/servicecontrol.sql-windows:4`
  * This is so that customers who are only interested in updates within a major can install the specific major only and not worry about breaking changes between major versions being automatically rolled out. Useful for auto upgrading containers in a *production* environment.
* `particular/servicecontrol.sql-windows:4.15.0` => `particular/servicecontrol.sql-windows:latest`
  * Primarily for developers wanting to use the latest version (`docker-compose up -d --build --force-recreate --renew-anon-volumes`)
  * This is only in the case where the release's major version is the same as the current latest major version.
    * If a fix is being backported to a previous major (e.g. for this example, if this fix was being applied to version `3.8.5`, the major of `3` is not greater than or equal to the current major of `4`) then the `:latest` tag will not be updated.
    * If a release targets the current latest major (e.g. if this release was version 4.15.0, while the current major of ServiceControl is `4.x.x`, then the major is greater than or equal to `4`) then the `:latest` tag is updated to match the version being released.

Once the tagging has been completed, the images are considered to be publicly released.
