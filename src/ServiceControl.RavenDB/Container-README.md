# Particular Software ServiceControl RavenDB

The `particular/servicecontrol-ravendb` image is part of the Particular Service Platform, which includes the following images:

| Image Name | Documentation | Purpose |
|------------|---------------|---------|
| [`particular/servicecontrol`](https://hub.docker.com/r/particular/servicecontrol) | [Instance Documentation](https://docs.particular.net/servicecontrol/servicecontrol-instances/)<br/>[Container Documentation](https://docs.particular.net/servicecontrol/servicecontrol-instances/deployment/containers) | The primary/error instance, which includes error handling and recoverability |
| [`particular/servicecontrol-audit`](https://hub.docker.com/r/particular/servicecontrol-audit) | [Instance Documentation](https://docs.particular.net/servicecontrol/audit-instances/)<br/>[Container Documentation](https://docs.particular.net/servicecontrol/audit-instances/deployment/containers) | The audit instance, which stores audit data, and can be scaled out to multiple audit instances |
| [`particular/servicecontrol-monitoring`](https://hub.docker.com/r/particular/servicecontrol-monitoring) | [Instance Documentation](https://docs.particular.net/servicecontrol/monitoring-instances/)<br/>[Container Documentation](https://docs.particular.net/servicecontrol/monitoring-instances/deployment/containers) | The monitoring instance, which tracks runtime information like throughput, queue length, and other metrics |
| [`particular/servicecontrol-ravendb`](https://hub.docker.com/r/particular/servicecontrol-ravendb) | [Container Documentation](https://docs.particular.net/servicecontrol/ravendb/containers) | The database used by the error/audit instances |
| [`particular/servicepulse`](https://hub.docker.com/r/particular/servicepulse) | [App Documentation](https://docs.particular.net/servicepulse/)<br/>[Container Documentation](https://docs.particular.net/servicepulse/containerization/) | The web application that provides a front end for recoverability and monitoring features |

This image is the database used by the [`particular/servicecontrol`](https://hub.docker.com/r/particular/servicecontrol) and [`particular/servicecontrol-audit`](https://hub.docker.com/r/particular/servicecontrol-audit) images, based on the official [RavenDB image](https://hub.docker.com/r/ravendb/ravendb).

The purpose of this image is to provide version parity between ServiceControl and database containers. Users can be sure that a given version of the `servicecontrol` and `servicecontrol-audit` container images have been tested with and are known to work with the matching version of `servicecontrol-ravendb`.

## Usage

This is the most basic way to start the container using `docker run`:

```shell
docker run -d --name servicecontrol-db \
    -v db-config:/etc/ravendb \
    -v db-data:/var/lib/ravendb/data \
    particular/servicecontrol-ravendb:latest
```

For all other usage information see the [official container documentation](https://docs.particular.net/servicecontrol/ravendb/containers).

_**IMPORTANT:**  A single database container should not be shared between multiple ServiceControl instances in production scenarios._

## Image tagging

### `latest` tag

This tag is primarily for developers wanting to use the latest version. If a release targets the current latest major or is a new major after the previous latest, then the `:latest` tag is applied to the image pushed to Docker Hub.

If the release is a patch release to a previous major, then the `:latest` tag will not be added.

### Version tags

We use [SemVer](http://semver.org/) for versioning. Release images pushed to Docker Hub will be tagged with the release version.

### Major version tag

The latest release within a major version will be tagged with the major version number only on images pushed to Docker Hub. This allows users to target a specific major version to help avoid the risk of incurring breaking changes between major versions.

### Minor version tag

The latest release within a minor version will be tagged with `{major}.{minor}` on images pushed to Docker Hub. This allows users to target the latest patch within a specific minor version.

## Image architecture

This image is a multi-arch image based on the [`ravendb/ravendb`](https://hub.docker.com/r/ravendb/ravendb) base image supporting `linux/arm64` and `linux/amd64`.