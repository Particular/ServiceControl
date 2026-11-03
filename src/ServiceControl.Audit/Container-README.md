# Particular Software ServiceControl Audit

The `particular/servicecontrol-audit` image is part of the Particular Service Platform, which includes the following images:

| Image Name | Documentation | Purpose |
|------------|---------------|---------|
| [`particular/servicecontrol`](https://hub.docker.com/r/particular/servicecontrol) | [Instance Documentation](https://docs.particular.net/servicecontrol/servicecontrol-instances/)<br/>[Container Documentation](https://docs.particular.net/servicecontrol/servicecontrol-instances/deployment/containers) | The primary/error instance, which includes error handling and recoverability |
| [`particular/servicecontrol-audit`](https://hub.docker.com/r/particular/servicecontrol-audit) | [Instance Documentation](https://docs.particular.net/servicecontrol/audit-instances/)<br/>[Container Documentation](https://docs.particular.net/servicecontrol/audit-instances/deployment/containers) | The audit instance, which stores audit data, and can be scaled out to multiple audit instances |
| [`particular/servicecontrol-monitoring`](https://hub.docker.com/r/particular/servicecontrol-monitoring) | [Instance Documentation](https://docs.particular.net/servicecontrol/monitoring-instances/)<br/>[Container Documentation](https://docs.particular.net/servicecontrol/monitoring-instances/deployment/containers) | The monitoring instance, which tracks runtime information like throughput, queue length, and other metrics |
| [`particular/servicecontrol-ravendb`](https://hub.docker.com/r/particular/servicecontrol-ravendb) | [Container Documentation](https://docs.particular.net/servicecontrol/storage/ravendb/containers) | The database for error/audit instances using RavenDB persistence |
| [`particular/servicepulse`](https://hub.docker.com/r/particular/servicepulse) | [App Documentation](https://docs.particular.net/servicepulse/)<br/>[Container Documentation](https://docs.particular.net/servicepulse/containerization/) | The web application that provides a front end for recoverability and monitoring features |

## Usage

The following minimal example uses `docker run` to host an Audit instance on Azure, with [Azure Service Bus](https://docs.particular.net/servicecontrol/transports.md#azure-service-bus) as the transport, Azure SQL Database for [SQL Server storage](https://docs.particular.net/servicecontrol/storage/sqlserver/), and [Azure Blob Storage](https://docs.particular.net/servicecontrol/storage/external-body/azure-blob.md) for message bodies:

```shell
docker run -d --name audit -p 44444:44444 \
    -e TRANSPORTTYPE=NetStandardAzureServiceBus \
    -e CONNECTIONSTRING="Endpoint=sb://my-namespace.servicebus.windows.net/;SharedAccessKeyName=...;SharedAccessKey=..." \
    -e STORAGETYPE=SQLServer \
    -e DATABASE_CONNECTIONSTRING="Server=tcp:my-server.database.windows.net,1433;Database=ServiceControl.Audit;User ID=...;Password=...;Encrypt=True" \
    -e MESSAGEBODY_STORAGETYPE=AzureBlob \
    -e MESSAGEBODY_AZURE_CONNECTIONSTRING="DefaultEndpointsProtocol=https;AccountName=mystorageaccount;AccountKey=...;EndpointSuffix=core.windows.net" \
    particular/servicecontrol-audit:latest
```

Each of these services can also use [managed identity](https://docs.particular.net/servicecontrol/transports.md#azure-service-bus-enabling-managed-identity) instead of the keys and passwords shown; see [SQL Server authentication](https://docs.particular.net/servicecontrol/storage/sqlserver/#authentication) and [Azure Blob Storage authentication](https://docs.particular.net/servicecontrol/storage/external-body/azure-blob.md#authentication).

### Storage

`STORAGETYPE` is required. Prepare the storage as the provider's guide describes before running setup.

| `STORAGETYPE` | Connection string environment variable | Configuration |
|---------------|----------------------------------------|---------------|
| `RavenDB` | `RAVENDB_CONNECTIONSTRING` | [RavenDB container setup](https://docs.particular.net/servicecontrol/storage/ravendb/containers) |
| `SQLServer` | `DATABASE_CONNECTIONSTRING` | [Configure SQL Server storage](https://docs.particular.net/servicecontrol/storage/sqlserver/) |
| `PostgreSQL` | `DATABASE_CONNECTIONSTRING` | [Configure PostgreSQL storage](https://docs.particular.net/servicecontrol/storage/postgresql/) |

With SQL Server or PostgreSQL, the database keeps only text message bodies up to `MAXBODYSIZETOSTORE` bytes. To keep larger and binary bodies, configure [external message body storage](https://docs.particular.net/servicecontrol/storage/external-body/) with `MESSAGEBODY_STORAGETYPE`. The [storage overview](https://docs.particular.net/servicecontrol/storage/#sharing-a-database) explains how Error and Audit instances can share a database.

These settings also accept the `SERVICECONTROL_AUDIT_` prefix, for example `SERVICECONTROL_AUDIT_STORAGETYPE` and `SERVICECONTROL_AUDIT_DATABASE_CONNECTIONSTRING`. Use prefixed settings when error and audit containers share environment configuration but need different persistence settings.

For all other usage information see the [official container documentation](https://docs.particular.net/servicecontrol/audit-instances/deployment/containers).

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

This image is a multi-arch image based on the [`mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-composite-extra`](https://mcr.microsoft.com/en-us/product/dotnet/aspnet/about) base image supporting `linux/arm64` and `linux/amd64`.

## Authors

This software, including this container image, is built and maintained by the team at Particular Software. See also the list of contributors who participated in this project.

## License

This project is licensed under the Reciprocal Public License 1.5 (RPL1.5) and commercial licenses are available - see the [source repository license file](https://github.com/Particular/ServiceControl/blob/master/LICENSE.md) for more information.