namespace ServiceControl.Persistence.RavenDB.CustomChecks
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using NServiceBus.CustomChecks;
    using ServiceControl.Infrastructure;
    using ServiceControl.Persistence.RavenDB;

    class CheckFreeDiskSpace(RavenPersisterSettings settings, ILogger<CheckFreeDiskSpace> logger) : CustomCheck("ServiceControl database", "Storage space", TimeSpan.FromMinutes(5))
    {
        public override Task<CheckResult> PerformCheck(CancellationToken cancellationToken = default)
        {
            logger.LogDebug("Check ServiceControl data drive space remaining custom check starting. Threshold {PercentageThreshold:P0}", percentageThreshold);

            if (!settings.UseEmbeddedServer)
            {
                return CheckResult.Pass;
            }

            if (dataPathRoot is null)
            {
                throw new Exception($"Unable to find the root of the data path {settings.DatabasePath}");
            }

            var dataDriveInfo = new DriveInfo(dataPathRoot);
            var availableFreeSpace = (decimal)dataDriveInfo.AvailableFreeSpace;
            var totalSpace = (decimal)dataDriveInfo.TotalSize;

            var percentRemaining = (decimal)dataDriveInfo.AvailableFreeSpace / dataDriveInfo.TotalSize;

            logger.LogDebug("Free space: {FreeSpaceTotalBytesFree:N0}B | Total: {FreeSpaceTotalBytesAvailable:N0}B | Remaining {PercentRemaining:P1}%", availableFreeSpace, totalSpace, percentRemaining);

            return percentRemaining > percentageThreshold
                ? CheckResult.Pass
                : CheckResult.Failed($"{percentRemaining:P0} disk space remaining on data drive '{dataDriveInfo.VolumeLabel} ({dataDriveInfo.RootDirectory})' on '{Environment.MachineName}'.");
        }

        public class Validation : IValidateOptions<RavenPersisterSettings> // TODO: Register!!
        {
            public ValidateOptionsResult Validate(string name, RavenPersisterSettings options)
            {
                var threshold = options.DataSpaceRemainingThreshold;

                if (threshold < 0)
                {
                    return ValidateOptionsResult.Fail($"{RavenPersistenceConfiguration.DataSpaceRemainingThresholdKey} is invalid, minimum value is 0.");
                }

                if (threshold > 100)
                {
                    return ValidateOptionsResult.Fail($"{RavenPersistenceConfiguration.DataSpaceRemainingThresholdKey} is invalid, maximum value is 100.");
                }

                return ValidateOptionsResult.Success;
            }
        }

        readonly string dataPathRoot = Path.GetPathRoot(settings.DatabasePath);
        readonly decimal percentageThreshold = settings.DataSpaceRemainingThreshold / 100m;

        public const int DataSpaceRemainingThresholdDefault = 20;
    }
}