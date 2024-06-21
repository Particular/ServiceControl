# Particular Software ServiceControl

This document describes basic usage and information related to the Particular ServiceControl images:

* `particular/servicecontrol`: The image for the error instance
* `particular/servicecontrol-audit`: The image for the audit instance
* `particular/servicecontrol-monitoring`: The image for the monitoring instance
* `particular/servicecontrol-ravendb`: The image for the database used by ServiceControl

Complete documentation of the ServiceControl container images can be found in the [Particular Software ServiceControl documentation](https://docs.particular.net/servicecontrol).

## Usage

The following is the most basic way to create ServiceControl containers using [Docker](https://www.docker.com/), assuming a RabbitMQ message broker also hosted in a Docker container using default `guest`/`guest` credentials:

```shell
# Run with setup entry point to create message queues, then exit and remove the container
docker run -e TransportType=RabbitMQ.QuorumConventionalRouting -e ConnectionString='host=host.docker.internal' -rm particular/servicecontrol:latest --setup
docker run -e TransportType=RabbitMQ.QuorumConventionalRouting -e ConnectionString='host=host.docker.internal' -rm particular/servicecontrol-audit:latest --setup
docker run -e TransportType=RabbitMQ.QuorumConventionalRouting -e ConnectionString="host=host.docker.internal" -rm particular/servicecontrol-monitoring:latest --setup

# Run one instance of the database
docker run -d -p 8080:8080 particular/servicecontrol-ravendb:latest-x64

# Run the instances in normal mode
docker run -d -p 33333:33333 -e TransportType=RabbitMQ.QuorumConventionalRouting -e ConnectionString='host=host.docker.internal' -e RavenDB_ConnectionString='http://host.docker.internal:8080' -e RemoteInstances='[{"api_uri":"http://host.docker.internal:44444/api"}]' particular/servicecontrol:latest
docker run -d -p 44444:44444 -e TransportType=RabbitMQ.QuorumConventionalRouting -e ConnectionString='host=host.docker.internal' -e RavenDB_ConnectionString='http://host.docker.internal:8080' particular/servicecontrol-audit:latest
docker run -d -p 33633:33633 -e TransportType=RabbitMQ.QuorumConventionalRouting -e ConnectionString="host=host.docker.internal" particular/servicecontrol-monitoring:latest
```

## Image tagging

### `latest` tag

This tag is primarily for developers wanting to use the latest version. If a release targets the current latest major or is a new major after the previous latest, then the `:latest` tag is applied to the image pushed to Docker Hub.

If the release is a patch release to a previous major, then the `:latest` tag will not be added.

### Version tags

We use [SemVer](http://semver.org/) for versioning. Release images pushed to Docker Hub will be tagged with the release version.

### Major version tag

The latest release within a major version will be tagged with the major version number only on images pushed to Docker Hub. This allows users to target a specific major version to help avoid the risk of incurring breaking changes between major versions.

## Image architecture

The `servicecontrol`, `servicecontrol-audit`, and `servicecontrol-monitoring` images are multi-arch images based on the `mcr.microsoft.com/dotnet/aspnet:8.0` base image supporting `linux/arm64`, `linux/arm`, and `linux/amd64`.

The `servicecontrol-ravendb` image is based on versions of the [`ravendb/ravendb`](https://hub.docker.com/r/ravendb/ravendb) image, and have separate tags for `-x64`, `-arm64v8`, and `-arm32v7`.

## Authors

This software, including this container image, is built and maintained by the team at Particular Software. See also the list of contributors who participated in this project.

## License

This project is licensed under the Reciprocal Public License 1.5 (RPL1.5) and commercial licenses are available - see the [source repository license file](https://github.com/Particular/ServiceControl/blob/master/LICENSE.md) for more information.