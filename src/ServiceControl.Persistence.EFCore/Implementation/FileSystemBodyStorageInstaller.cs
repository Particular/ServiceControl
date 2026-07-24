namespace ServiceControl.Persistence.EFCore.Implementation;

using ServiceControl.Persistence;
using ServiceControl.Persistence.EFCore.Abstractions;

public class FileSystemBodyStorageInstaller(EFPersisterSettings settings) : IBodyStorageInstaller
{
    public Task Provision(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(settings.MessageBodyStoragePath!);

        return Task.CompletedTask;
    }
}
