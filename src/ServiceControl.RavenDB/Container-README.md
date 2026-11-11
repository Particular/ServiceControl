# Particular Software ServiceControl RavenDB

The `particular/servicecontrol-ravendb` image is part of the Particular Service Platform, which includes the following images:

| Image Name | Documentation | Purpose |
|------------|---------------|---------|
| [`particular/servicecontrol`](https://hub.docker.com/r/particular/servicecontrol) | [Instance Documentation](https://docs.particular.net/servicecontrol/servicecontrol-instances/)<br/>[Container Documentation](https://docs.particular.net/servicecontrol/servicecontrol-instances/deployment/containers) | The primary/error instance, which includes error handling and recoverability |
| [`particular/servicecontrol-audit`](https://hub.docker.com/r/particular/servicecontrol-audit) | [Instance Documentation](https://docs.particular.net/servicecontrol/audit-instances/)<br/>[Container Documentation](https://docs.particular.net/servicecontrol/audit-instances/deployment/containers) | The audit instance, which stores audit data, and can be scaled out to multiple audit instances |
| [`particular/servicecontrol-monitoring`](https://hub.docker.com/r/particular/servicecontrol-monitoring) | [Instance Documentation](https://docs.particular.net/servicecontrol/monitoring-instances/)<br/>[Container Documentation](https://docs.particular.net/servicecontrol/monitoring-instances/deployment/containers) | The monitoring instance, which tracks runtime information like throughput, queue length, and other metrics |
| [`particular/servicecontrol-ravendb`](https://hub.docker.com/r/particular/servicecontrol-ravendb) | [Container Documentation](https://docs.particular.net/servicecontrol/ravendb/containers) | The database used by the error/audit instances |
| [`particular/servicepulse`](https://hub.docker.com/r/particular/servicepulse) | [App Documentation](https://docs.particular.net/servicepulse/)<br/>[Container Documentation](https://docs.particular.net/servicepulse/containerization/) | The web application that provides a front end for recoverability and monitoring features |

This image is the database used by the [`particular/servicecontrol`](https://hub.docker.com/r/particular/servicecontrol) and [`particular/servicecontrol-audit`](https://hub.docker.com/r/particular/servicecontrol-audit) images, based on the official [RavenDB image](https://hub.docker.com/r/ravendb/ravendb). The default tags run RavenDB 7.2, and the tags with the `-ravendb6` suffix run RavenDB 6.2.

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

## RavenDB 6 and RavenDB 7

ServiceControl instances use the RavenDB 7 persister by default (`PersistenceType=RavenDB7`), which requires a RavenDB 7.2 server. Instances that use `PersistenceType=RavenDB` (RavenDB 6 persister, removed in ServiceControl 8) should use the RavenDB 6.2 server, which does not convert the data.

| Tag | RavenDB server | `PersistenceType` of the instances |
|-----|----------------|------------------------------------|
| `latest`, `{major}`, `{major}.{minor}`, `{version}` | 7.2 | `RavenDB7` (default) |
| `latest-ravendb6`, `{major}-ravendb6`, `{major}.{minor}-ravendb6`, `{version}-ravendb6` | 6.2 | `RavenDB` |

To stay on RavenDB 6, use the `-ravendb6` tag together with `PersistenceType=RavenDB` on the `particular/servicecontrol` and `particular/servicecontrol-audit` containers:

```shell
docker run -d --name servicecontrol-db \
    -v db-config:/etc/ravendb \
    -v db-data:/var/lib/ravendb/data \
    particular/servicecontrol-ravendb:latest-ravendb6
```

### Storage conversion guard

A RavenDB 7 server converts a RavenDB 6 data directory one-way, and RavenDB 6 cannot open the data afterwards. On a large audit database the conversion can take more than a day, during which the database is unavailable. Because deployments usually use one tag for all images, changing that tag from a 6.x to a 7.x version would otherwise start the conversion without warning.

To prevent this, the image checks the data directory (`/var/lib/ravendb/data`) before it starts RavenDB, using the marker file `servicecontrol-ravendb7.marker`:

* The default (RavenDB 7) image starts on an empty data directory and writes the marker file.
* The default image refuses to start, with a non-zero exit code, on a data directory that contains databases but no marker file. To convert the data, back it up and set the environment variable `RAVENDB7_ACCEPT_STORAGE_CONVERSION=true` on the database container. The image then writes the marker file and starts, and RavenDB converts the data. The variable can be removed after the first start.
* The `-ravendb6` image refuses to start on a data directory that has the marker file, because RavenDB 6 cannot open data that RavenDB 7 has taken over. To go back to RavenDB 6, restore a backup taken before the conversion.

The image also refuses to start when it cannot read the data directory, or cannot write the marker file. In that case, make sure the owner and group of the data directory are set to ID 999.

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

### RavenDB 6 tags

Every tag described above is also published with the `-ravendb6` suffix, for example `latest-ravendb6`, `7-ravendb6`, `7.0-ravendb6` and `7.0.0-ravendb6`. These images run RavenDB 6.2 and are for deployments that stay on `PersistenceType=RavenDB`.

## Image architecture

Both tag lines are multi-arch images based on the [`ravendb/ravendb`](https://hub.docker.com/r/ravendb/ravendb) base image supporting `linux/arm64` and `linux/amd64`.