# Packaging

Each product (ServiceControl, ServiceControl.Monitoring, and ServiceControl.Audit) is packaged into its own versioned zip file in the `zip` folder. These zip files are included as resources in the ServiceControlInstaller.Engine project. ServiceControl Management and the PowerShell module use them to create new app instances.

The zip files minimize duplication to control the overall file size of each installer. The zip file for each app contains only the specific app code and the persistence code unique to that application.

- `ServiceControl.zip`
- `ServiceControl.Audit.zip`
- `ServiceControl.Monitoring.zip`
- `InstanceShared.zip` - Contains the transport assemblies that are shared by each instance, as well as any other assemblies that are shared across all 3 instance zip files, such as `NServiceBus.Core.dll`.
- `RavenDBServer.zip` - Contains the RavenDB 6.2 server, for the `RavenDB` persister (alias `RavenDB6`)
- `RavenDB7Server.zip` - Contains the RavenDB 7.2 server, for the `RavenDB7` persister

Both server archives exclude the native Kafka dependencies `librdkafka.dll`, `librdkafkacpp.dll`, and `libcurl.dll`, and the ONNX runtime and `LocalEmbeddings` files. ServiceControl does not configure RavenDB's Kafka ETL or sink tasks. The Windows installation engine tests extract each archive and verify document persistence and indexing across a server restart without these libraries.

## RavenDB servers

ServiceControl ships two RavenDB persisters. `RavenDB7` runs on RavenDB 7.2 and is the persister for new instances. `RavenDB` runs on RavenDB 6.2 and stays the persister of existing instances until ServiceControl 8 removes it. Both persisters use the same assembly names and the same manifest `TypeName`, and each finds its embedded server in a `RavenDBServer` folder next to its own assembly.

A RavenDB 7.2 server that opens RavenDB 6.2 data converts it one way, which can take days. The installation engine therefore installs the server that matches the instance's persister, in one place (`RavenDBServerPackage`), whether it creates, upgrades, or force-upgrades the instance:

- An instance on `RavenDB7` gets `RavenDB7Server.zip` in `Persisters\RavenDB7\RavenDBServer`.
- Every other instance gets `RavenDBServer.zip` in `Persisters\RavenDB\RavenDBServer`. This includes SQL Server and PostgreSQL instances, because the planned migration from RavenDB to SQL reads the old 6.2 database through the `RavenDB` persister after the persistence type has changed.

An upgrade never changes the persistence type of an instance. A force upgrade of a ServiceControl 4 instance on RavenDB 3.5 abandons the old database, so it moves the instance to `RavenDB7`.

The persister projects place the servers in `deploy/RavenDBServer` (6.2) and `deploy/RavenDB7Server` (7.2) when they build on a developer machine. A CI or `WindowsSelfContained` build skips that step. `tools/download-ravendb-server.ps1` then downloads the self-contained 6.2 server (the `RavenDB6Version` property in `src/Directory.Packages.props`) and the self-contained 7.2 server (the central `RavenDB.Embedded` version) into those folders. The `ServiceControlInstaller.Packaging` project fails with an error that names the missing folder when either folder does not exist.

The `ServiceControlInstaller.Engine` project compares the server an instance runs today with the archive bundled for the same persister to decide whether an upgrade converts the database (`RavenDBStorageSchema` and `RavenDBStorageUpgrade`). Upgrading an instance on `RavenDB` stays on 6.2 and does not convert. `RavenDBStorageUpgrade.ConvertsDatabase(instance, target)` answers the same question for a move to another persister, for example from `RavenDB` to `RavenDB7`.

## The mechanics

The [Microsoft.Build.Artifacts](https://github.com/microsoft/MSBuildSdks/tree/main/src/Artifacts) package defines artifacts that the solution build places into the `deploy` folder. Each project that contributes artifacts has an `Artifact` definition in its project file.
To ensure the correct build order, the `ServiceControlInstaller.Packaging` project needs a `ProjectReference` to every project that has an artifact definition.

Every project that uses the artifacts needs a build-order `ProjectReference` to the `ServiceControlInstaller.Packaging` project. The projects that use the artifacts are:

- The `ServiceControlInstaller.Engine` project to create the above-mentioned required zip files
- The `Particular.PlatformSample.ServiceControl` project to create the Platform sample required NuGet package

The Platform sample package contains the `Persisters\RavenDB7` folder only, because the Platform sample always creates new databases. A consumer must configure the `RavenDB7` persistence type.

## Assembly version mismatches

There can be an issue when the main instance folder and the selected transport/persister component each have a copy of the same assembly but reference different versions. At install time, one of the two versions is copied into the instance binary folder, and the instance may fail at runtime.

To prevent this, the unit test `DeploymentPackageTests.DuplicateAssemblyShouldHaveMatchingVersions` checks assemblies that could be deployed twice. The test passes if their versions match. Otherwise it fails with:

```
  Component assembly version mismatch detected
  Expected: <empty>
  But was:  < "System.Memory.dll has a different version in Instance/ServiceControl compared to Transports/RabbitMQ. Add the package to Directory.Packages.props to ensure the same version is used everywhere: 4.6.31308.01 | 4.6.28619.01", "System.Memory.dll has a different version in Transports/RabbitMQ compared to Instance/ServiceControl. Add the package to Directory.Packages.props to ensure the same version is used everywhere: 4.6.28619.01 | 4.6.31308.01" >
```

### How to resolve

The repo uses [NuGet central package management](https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management) so that each project uses the same version of a dependency. When the test fails with a version mismatch, add the package that provides the assembly to the `Versions to pin transitive references` ItemGroup in the `Directory.packages.props` file.