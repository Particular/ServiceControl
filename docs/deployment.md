# Deployment

All ServiceControl deployment options listed below rely on the zip files produced by [packaging](packaging.md).

## ServiceControll installer

A [windows installer is provided for download](https://docs.particular.net/servicecontrol/installation) containing both ServiceControl Management Utility (SCMU) and [Powershell commands](https://docs.particular.net/servicecontrol/powershell).

When installing an instance using one of the above method instance files, persister (if applicable) and transport files are unzipped into the same folder.

A configuration file generated based on:

- User input (and defaults)
- Contents of the `persister.manifest` file of the selected persiser

## NuGet package

The binaries are shipped to the [PlatformSample](https://github.com/Particular/Particular.PlatformSample) via the `Particular.PlatformSample.ServiceControl` NuGet package with transport hardcoded to `LearningTransport` and persister hardcoded to `RavenDB 3.5`.

NOTE: The platform sample runs all instance in the same process the same transport and persister has to be used.

## Docker

NOTE: Docker isn't currently officially supported but we do have [a sample showing how to use it](https://docs.particular.net/samples/platformtools-docker-compose/).

Multiple docker images, windows only, are generated using the following matrix [{InstanceType}, {TransportType}, {Init|NoInit}] where `Init` means that the instance is started in setup mode and then shutdown to support creating queues, database artefacts etc. All images are pushed to [DockerHub](https://hub.docker.com/) during deployment.

All required configuration values are defaulted in the `dockerfile` but can be overrided via environment variables. To simulate an instance running in docker the `{instance}.exe` passing in the `--portable` option.

NOTE: In CI the binaries for the docker files are extracted from the zipfile produced by packaging while locally they are pulled from the bin folders. https://github.com/Particular/ServiceControl/issues/3184 tracks fixing this.
