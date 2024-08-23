# Particular Software ServiceControl RavenDB

The `particular/servicecontrol-ravendb` image is part of the Particular Service Platform, which includes the following images:

| Image Name | Documentation | Purpose |
|------------|---------------|---------|
| [`particular/servicecontrol`](https://hub.docker.com/r/particular/servicecontrol) | [Instance Documentation](https://docs.particular.net/servicecontrol/servicecontrol-instances/)<br/>[Container Documentation](https://docs.particular.net/servicecontrol/servicecontrol-instances/deployment/containers) | The primary/error instance, which includes error handling and recoverability |
| [`particular/servicecontrol-audit`](https://hub.docker.com/r/particular/servicecontrol-audit) | [Instance Documentation](https://docs.particular.net/servicecontrol/audit-instances/)<br/>[Container Documentation](https://docs.particular.net/servicecontrol/audit-instances/deployment/containers) | The audit instance, which stores audit data, and can be scaled out to multiple audit instances |
| [`particular/servicecontrol-monitoring`](https://hub.docker.com/r/particular/servicecontrol-monitoring) | [Instance Documentation](https://docs.particular.net/servicecontrol/monitoring-instances/)<br/>[Container Documentation](https://docs.particular.net/servicecontrol/monitoring-instances/deployment/containers) | The monitoring instance, which tracks runtime information like throughput, queue length, and other metrics |
| [`particular/servicecontrol-ravendb`](https://hub.docker.com/r/particular/servicecontrol-ravendb) | [Container Documentation](https://docs.particular.net/servicecontrol/ravendb/containers) | The database used by the error/audit instances |
| [`particular/servicepulse`](https://hub.docker.com/r/particular/servicecontrol-ravendb) | [App Documentation](https://docs.particular.net/servicepulse/)<br/>[Container Documentation](https://docs.particular.net/servicepulse/containerization/) | The web application that provides a front end for recoverability and monitoring features |

This image is the database used by the [`particular/servicecontrol`](https://hub.docker.com/r/particular/servicecontrol) and [`particular/servicecontrol-audit`](https://hub.docker.com/r/particular/servicecontrol-audit) images, based on the official [RavenDB image](https://hub.docker.com/r/ravendb/ravendb).

The purpose of this image is to provide version parity between ServiceControl and database containers. Users can be sure that a given version of the `servicecontrol` and `servicecontrol-audit` container images have been tested with and are known to work with the matching version of `servicecontrol-ravendb`.

## Usage

This is the most basic way to start the container using `docker run`:

```shell
docker run -d --name servicecontrol-db \
    -v <DATA_DIRECTORY>:/opt/RavenDB/Server/RavenData \
    particular/servicecontrol-ravendb:latest
```

For all other usage information see the [official container documentation](https://docs.particular.net/servicecontrol/ravendb/containers).

_**IMPORTANT:**  A single database container should not be shared between multiple ServiceControl instances in production scenarios._