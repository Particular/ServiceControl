# Particular Software ServiceControl RavenDB

This image is the database used by the [`particular/servicecontrol`](https://hub.docker.com/r/particular/servicecontrol) and [`particular/servicecontrol`](https://hub.docker.com/r/particular/servicecontrol-audit) images, based on the official [RavenDB image](https://hub.docker.com/r/ravendb/ravendb).

The purpose of this image is to provide version parity between ServiceControl and database containers. Users can be sure that a given version of the `servicecontrol` and `servicecontrol-audit` container images have been tested with and are known to work with the matching version of `servicecontrol-ravendb`.

## Usage

This is the most basic way to start the container using `docker run`:

```shell
docker run -d -p 8080:8080 particular/servicecontrol-ravendb:latest
```

_**IMPORTANT:**  A single database container should not be shared between multiple ServiceControl instances in production scenarios._

## Additional options

To customize the container's operation, refer to the [base image documentation](https://hub.docker.com/r/ravendb/ravendb).