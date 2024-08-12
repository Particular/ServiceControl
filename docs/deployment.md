# Deployment

All ServiceControl deployment options listed below rely on the files produced by [packaging](packaging.md). During packaging, deployment binaries are created in the `deploy` directory, some of which are packaged into zip files in the `zips` folder, and other deployment artifacts are built from these pieces.

## ServiceControl installer

The zips are packaged as embedded resources in the ServiceControl Management Utility (the "installer").

When installing an instance using one of the above method instance files, the following binaries are unzipped together to form a complete instance:

- Application & persistence files
- Selected transport
- RavenDB server files (ServiceControl and Audit only)

A configuration file generated based on:

- User input (and defaults)
- Contents of the `transport.manifest` and `persister.manifest` file for the selected transport and persister.

## PowerShell module

The PowerShell module is built during the release workflow in the `deploy/PowerShellModules` directory. During the release process, this is pushed to the [PowerShell Gallery](https://www.powershellgallery.com/packages/Particular.ServiceControl.Management/).

## Docker images

The release workflow builds multi-arch Docker images with a specific version tag and pushes the images to the [GitHub Container Registry(https://github.com/Particular/ServiceControl/packages)]. Packages are distributed to Docker Hub with multiple tags (i.e. `latest`, `5`, `5.4`, and `5.4.0`) during the release process using the [push container images workflow](/.github/workflows/push-container-images.yml).

Images are availble in the following Docker Hub repositories:

- [servicecontrol](https://hub.docker.com/r/particular/servicecontrol)
- [servicecontrol-audit](https://hub.docker.com/r/particular/servicecontrol-audit)
- [servicecontrol-monitoring](https://hub.docker.com/r/particular/servicecontrol-monitoring)
- [servicecontrol-ravendb](https://hub.docker.com/r/particular/servicecontrol-ravendb)

## NuGet package

The binaries are shipped to the [PlatformSample](https://github.com/Particular/Particular.PlatformSample) via the `Particular.PlatformSample.ServiceControl` NuGet package with transport hardcoded to `LearningTransport` and persister hardcoded to `RavenDB`. In order to avoid shipping RavenDB binaries twice, the platform sample hosts its own RavenDB.Embedded instance and connects both the ServiceControl and Audit instances to it.


