namespace ServiceControl.Persistence.EFCore.Implementation;

using ServiceControl.Persistence;
using ServiceControl.Persistence.EFCore.Abstractions;

public class FileSystemBodyStorageInstaller(FileSystemBodyStorageSettings settings) : IBodyStorageInstaller
{
    public Task Provision(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(settings.StoragePath);

        return Task.CompletedTask;
    }
}
