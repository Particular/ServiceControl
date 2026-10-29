namespace ServiceControl.Persistence.EFCore.Implementation.BodyStorage;

using ServiceControl.Persistence;
using ServiceControl.Persistence.EFCore.Abstractions;

public class FileSystemBodyStorageInstaller(FileSystemBodyStorageSettings settings) : IBodyStorageInstaller
{
    public async Task Provision(CancellationToken cancellationToken = default)
    {
        try
        {
            Directory.CreateDirectory(settings.StoragePath);

            var probePath = Path.Combine(settings.StoragePath, $".write-probe-{Guid.NewGuid():N}");
            await using var probe = new FileStream(probePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.DeleteOnClose);
            await probe.WriteAsync(new byte[] { 1 }, cancellationToken).ConfigureAwait(false);
            await probe.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException($"Message body storage path '{settings.StoragePath}' is not writable. Ensure the directory can be created and the service account can write to it.", ex);
        }
    }
}
