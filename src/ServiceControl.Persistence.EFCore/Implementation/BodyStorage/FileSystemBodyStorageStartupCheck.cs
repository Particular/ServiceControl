namespace ServiceControl.Persistence.EFCore.Implementation.BodyStorage;

using ServiceControl.Infrastructure.StartupChecks;
using ServiceControl.Persistence.EFCore.Abstractions;

sealed class FileSystemBodyStorageStartupCheck(FileSystemBodyStorageSettings settings) : IStartupCheck
{
    public async Task Check(CancellationToken cancellationToken = default)
    {
        try
        {
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
            throw new InvalidOperationException($"Message body storage path '{settings.StoragePath}' is not writable. Ensure the directory exists and the service account can write to it. Run --setup on the normal instance to create the directory.", ex);
        }
    }
}
