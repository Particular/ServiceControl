# Particular Software ServiceControl

The `particular/servicecontrol` image is part of the Particular Service Platform, which includes the following images:

| Image Name | Documentation | Purpose |
|------------|---------------|---------|
| [`particular/servicecontrol`](https://hub.docker.com/r/particular/servicecontrol) | [Instance Documentation](https://docs.particular.net/servicecontrol/servicecontrol-instances/)<br/>[Container Documentation](https://docs.particular.net/servicecontrol/servicecontrol-instances/deployment/containers) | The primary/error instance, which includes error handling and recoverability |
| [`particular/servicecontrol-audit`](https://hub.docker.com/r/particular/servicecontrol-audit) | [Instance Documentation](https://docs.particular.net/servicecontrol/audit-instances/)<br/>[Container Documentation](https://docs.particular.net/servicecontrol/audit-instances/deployment/containers) | The audit instance, which stores audit data, and can be scaled out to multiple audit instances |
| [`particular/servicecontrol-monitoring`](https://hub.docker.com/r/particular/servicecontrol-monitoring) | [Instance Documentation](https://docs.particular.net/servicecontrol/monitoring-instances/)<br/>[Container Documentation](https://docs.particular.net/servicecontrol/monitoring-instances/deployment/containers) | The monitoring instance, which tracks runtime information like throughput, queue length, and other metrics |
| [`particular/servicecontrol-ravendb`](https://hub.docker.com/r/particular/servicecontrol-ravendb) | [Container Documentation](https://docs.particular.net/servicecontrol/storage/ravendb/containers) | The database for error/audit instances using RavenDB persistence |
| [`particular/servicepulse`](https://hub.docker.com/r/particular/servicepulse) | [App Documentation](https://docs.particular.net/servicepulse/)<br/>[Container Documentation](https://docs.particular.net/servicepulse/containerization/) | The web application that provides a front end for recoverability and monitoring features |

## Usage

The following minimal example uses `docker run` to host an Error instance on AWS, with [Amazon SQS](https://docs.particular.net/servicecontrol/transports.md#amazon-sqs) as the transport, Amazon Aurora PostgreSQL for [PostgreSQL storage](https://docs.particular.net/servicecontrol/storage/postgresql/), and [Amazon S3](https://docs.particular.net/servicecontrol/storage/external-body/amazon-s3.md) for message bodies:

```shell
docker run -d --name servicecontrol -p 33333:33333 \
    -e AWS_REGION=us-east-1 \
    -e TRANSPORTTYPE=AmazonSQS \
    -e CONNECTIONSTRING="Region=us-east-1" \
    -e STORAGETYPE=PostgreSQL \
    -e DATABASE_CONNECTIONSTRING="Host=my-cluster.cluster-xxxxxxxxxxxx.us-east-1.rds.amazonaws.com;Database=ServiceControl;Username=servicecontrol;Token Authentication=AwsRdsIam;SSL Mode=Require" \
    -e MESSAGEBODY_STORAGETYPE=S3 \
    -e MESSAGEBODY_S3_BUCKETNAME=my-servicecontrol-bodies \
    -e REMOTEINSTANCES='[{"api_uri":"http://audit:44444/api"}]' \
    -e ENABLEINTEGRATEDSERVICEPULSE="true" \
    particular/servicecontrol:latest
```

The example contains no AWS keys or database password. SQS, S3, and the [IAM database login](https://docs.particular.net/servicecontrol/storage/postgresql/#authentication-token-authentication) use the AWS SDK default credential chain, such as an IAM role attached to the host or task, and the region set in `AWS_REGION`.

### Storage

`STORAGETYPE` is required. Prepare the storage as the provider's guide describes before running setup.

| `STORAGETYPE` | Connection string environment variable | Configuration |
|---------------|----------------------------------------|---------------|
| `RavenDB` | `RAVENDB_CONNECTIONSTRING` | [RavenDB container setup](https://docs.particular.net/servicecontrol/storage/ravendb/containers) |
| `SQLServer` | `DATABASE_CONNECTIONSTRING` | [Configure SQL Server storage](https://docs.particular.net/servicecontrol/storage/sqlserver/) |
| `PostgreSQL` | `DATABASE_CONNECTIONSTRING` | [Configure PostgreSQL storage](https://docs.particular.net/servicecontrol/storage/postgresql/) |

SQL Server and PostgreSQL also require [external message body storage](https://docs.particular.net/servicecontrol/storage/external-body/), selected with `MESSAGEBODY_STORAGETYPE`. The [storage overview](https://docs.particular.net/servicecontrol/storage/#sharing-a-database) explains how Error and Audit instances can share a database.

These settings also accept the `SERVICECONTROL_` prefix, for example `SERVICECONTROL_STORAGETYPE` and `SERVICECONTROL_DATABASE_CONNECTIONSTRING`.

For all other usage information see the [official container documentation](https://docs.particular.net/servicecontrol/servicecontrol-instances/deployment/containers).

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