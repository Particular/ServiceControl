namespace ServiceControl.Persistence.EFCore.Implementation.BodyStorage;

using ServiceControl.Persistence;
using ServiceControl.Persistence.BodyStorage;

public class FileSystemBodyStorageInstaller(FileSystemBodyStorageSettings settings) : IBodyStorageInstaller
{
    public Task Provision(CancellationToken cancellationToken = default) =>
        BodyStorageProvisioner.Provision(settings, cancellationToken);
}
