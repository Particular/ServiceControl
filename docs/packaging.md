# Packaging

Each product (ServiceControl, ServiceControl.Monitoring and ServiceControl.Audit), is packaged into its own versioned zip file in the `zip` folder. These zip files are included as resources in the ServiceControlInstaller.Engine project, to be used to create new app instances from both ServiceControl Management as well as the PowerShell module.

The zip files are crafted to minimize duplication in order to control the overall file size of each installer. The zips for each app contain only the specific app code as well as persistence code unique to that application.

- `ServiceControl.zip`
- `ServiceControl.Audit.zip`
- `ServiceControl.Monitoring.zip`
- `InstanceShared.zip` - Contains the transport assemblies that are shared by each instance
- `RavenDBServer.zip` - Contains the RavenDB server assemblies used by ServiceControl and Monitoring instances

## The mechanics

The [Microsoft.Build.Artifacts](https://github.com/microsoft/MSBuildSdks/tree/main/src/Artifacts) package is used to define artifacts that are placed into the `deploy` folder when the solution is built. Each project that contributes artifacts has an `Artifact` definition in its project file.
To ensure proper build ordering, the `ServiceControlInstaller.Packaging` project needs to have a `ProjectReference` to every project that has an artifact definition.

Every project that uses the artifacts then has to have a build ordering `ProjectReference` to the `ServiceControlInstaller.Packaging` project. The projects that use the artifacts are:

- The `ServiceControlInstaller.Engine` project to create the above-mentioned required zip files
- The `Particular.PlatformSample.ServiceControl` project to create the Platform sample required NuGet package

## Assembly version mismatches

There can be an issue when the main instance folder and the selected transport/persister component each have a copy of the same assembly but reference different versions. At install time, one version or the other will be copied into the instance binary folder and things may break unexpectedly at runtime.

To prevent this, the unit test `DeploymentPackageTests.DuplicateAssemblyShouldHaveMatchingVersions` tests if duplicated assemblies might be deployed. If their versions match, then the test passes. If not then the test will fail with:

```
  Component assembly version mismatch detected
  Expected: <empty>
  But was:  < "System.Memory.dll has a different version in Instance/ServiceControl compared to Transports/RabbitMQ. Add the package to Directory.Packages.props to ensure the same version is used everywhere: 4.6.31308.01 | 4.6.28619.01", "System.Memory.dll has a different version in Transports/RabbitMQ compared to Instance/ServiceControl. Add the package to Directory.Packages.props to ensure the same version is used everywhere: 4.6.28619.01 | 4.6.31308.01" >
```

### How to resolve

The repo uses [NuGet central package management](https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management) to ensure that the same version of dependencies are used in each project. When a test fails with a version mismatch, add the package that provides the assembly to the `Versions to pin transitive references` ItemGroup in the `Directory.packages.props` file.