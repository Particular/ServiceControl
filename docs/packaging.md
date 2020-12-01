# Packaging

Each product is packaged into it's own versioned zip file in the `./zip` folder, and these are included in the installation package. When ServiceControl Management installs a new instance, it uses the files in these zips. Each zip contain two top-level folders

- `/Transports` contains a folder for each supported transport
- `/<product>` contains the instance files specific to the type of instance
  - `/ServiceControl`
  - `/ServiceControl.Audit`
  - `/ServiceControl.Monitoring`
  
 When SCMU installs a product it unzips the main instance files, and the transport files into the same folder

## Assembly Mismatches

There can be an issue when the main instance folder and the selected transport each have a copy of the same assembly. This can happen when the transport package and the instance each reference the same dependency, but select different versions. At install time, one version or the other will be copied into the instance binary folder and things may break unexpectedly at runtime.

To prevent this, there is a unit test (`DeploymentPackageTests.DuplicatAssemblyFileSizesShouldMatch` that tests the generate zip files to look for duplicated assemblies. If their file sizes match, then the test passes. If the file sizes do not match, then the test will fail with a message like the following.

```
  File sizes should match the ones in the ServiceControl.Audit folder. Check versions of dependencies.
  Expected: <empty>
  But was:  < "Transports/AzureStorageQueue/System.Runtime.CompilerServices.Unsafe.dll", "Transports/AzureStorageQueue/System.Threading.Tasks.Extensions.dll" >

   at Tests.DeploymentPackageTests.DuplicateAssemblyFileSizesShouldMatch() in /_/src/ServiceControlInstaller.Packaging.UnitTests/DeploymentPackageTests.cs:line 19
```

> File sizes should match the ones in the **ServiceControl.Audit** folder. Check versions of dependencies.

This is telling you that the mismatch is happing in the ServiceControl.Audit zip.

> But was:  < "Transports/AzureStorageQueue/System.Runtime.CompilerServices.Unsafe.dll", "Transports/AzureStorageQueue/System.Threading.Tasks.Extensions.dll" >

This is telling you the assemblies that are different from the ones found in the ServiceControl.Audit folder.

### How to resolve

When a mismatch occurs, we need to make sure that the versions of the dependencies match between the transport package and the ServiceControl package. 

There are two mechanisms for doing that:

1. Add an explicit version of the package as a dependency on the transport package that matches the version being used by ServiceControl. This is the preferred option, as Dependabot will pick up when a new version of the dependency has been released and raise a PR for us to test and review.
2. [Exclude a specific assembly from being included in any transport folder during the packaging process](https://github.com/Particular/ServiceControl/pull/1735/files#diff-181a8bea53d298736c8183d4d5821665e2ec3c854e5f7a4f7e8694b4cddc4b3f). This is a legacy option and should only be used for packages that most transports rely on. This will cause a mismatch between what is automatically tested and what goes into the deployment package.
