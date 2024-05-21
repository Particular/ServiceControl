# Deployment

All ServiceControl deployment options listed below rely on the files produced by [packaging](packaging.md).

## ServiceControl installer

A [windows installer is provided for download](https://docs.particular.net/servicecontrol/installation) containing both ServiceControl Management Utility (SCMU) and [Powershell commands](https://docs.particular.net/servicecontrol/powershell).

When installing an instance using one of the above method instance files, persister (if applicable) and transport files are unzipped into the same folder.

A configuration file generated based on:

- User input (and defaults)
- Contents of the `persister.manifest` file of the selected persister

## NuGet package

The binaries are shipped to the [PlatformSample](https://github.com/Particular/Particular.PlatformSample) via the `Particular.PlatformSample.ServiceControl` NuGet package with transport hardcoded to `LearningTransport` and persister hardcoded to `In Memory`.

NOTE: The platform sample runs all instance in the same process the same transport and persister has to be used.

## Docker images

TODO: Add details for Linux containers
