namespace ServiceControl.Persistence.EFCore.Implementation.BodyStorage;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NServiceBus.CustomChecks;
using ServiceControl.Persistence.EFCore.Abstractions;

public class FileSystemBodyStorageCustomCheck(FileSystemBodyStorageSettings settings, IDriveSpaceProvider driveSpaceProvider, ILogger<FileSystemBodyStorageCustomCheck> logger)
    : CustomCheck("ServiceControl body storage", "Storage space", TimeSpan.FromMinutes(15))
{
    public override Task<CheckResult> PerformCheck(CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Check ServiceControl body storage drive space remaining custom check starting. Threshold {PercentageThreshold:P0}", percentageThreshold);

        if (string.IsNullOrEmpty(dataPathRoot))
        {
            return CheckResult.Failed($"Unable to find the root of the message body storage path '{settings.StoragePath}'. An absolute path is required.");
        }

        DriveSpace driveSpace;

        try
        {
            driveSpace = driveSpaceProvider.GetDriveSpace(dataPathRoot);
        }
        catch (Exception ex)
        {
            // Custom checks report an exception as an opaque failure, so the reason is captured here instead.
            logger.LogError(ex, "Unable to read the free space of drive '{DataPathRoot}' for the message body storage path '{StoragePath}'", dataPathRoot, settings.StoragePath);

            return CheckResult.Failed($"Unable to read the free space of drive '{dataPathRoot}' for the message body storage path '{settings.StoragePath}' on '{Environment.MachineName}': {ex.Message}");
        }

        if (driveSpace.TotalSize <= 0)
        {
            return CheckResult.Failed($"Unable to determine the size of drive '{driveSpace.Name}' for the message body storage path '{settings.StoragePath}' on '{Environment.MachineName}'.");
        }

        var percentRemaining = (decimal)driveSpace.AvailableFreeSpace / driveSpace.TotalSize;

        logger.LogDebug("Free space: {FreeSpaceTotalBytesFree:N0}B | Total: {FreeSpaceTotalBytesAvailable:N0}B | Remaining {PercentRemaining:P1}", driveSpace.AvailableFreeSpace, driveSpace.TotalSize, percentRemaining);

        return percentRemaining > percentageThreshold
            ? CheckResult.Pass
            : CheckResult.Failed($"{percentRemaining:P0} disk space remaining on the message body storage drive '{driveSpace.Name}' ({settings.StoragePath}) on '{Environment.MachineName}'.");
    }

    readonly string? dataPathRoot = Path.GetPathRoot(settings.StoragePath);
    readonly decimal percentageThreshold = settings.DataSpaceRemainingThreshold / 100m;
}
