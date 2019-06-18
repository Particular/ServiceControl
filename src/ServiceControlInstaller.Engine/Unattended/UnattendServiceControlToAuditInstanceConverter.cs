namespace ServiceControlInstaller.Engine.Unattended
{
    using Accounts;
    using Configuration.ServiceControl;
    using Instances;
    using Validation;

    public class UnattendServiceControlToAuditInstanceConverter
    {
        readonly ILogging log;
        UnattendServiceControlInstaller serviceControlInstaller;
        UnattendAuditInstaller auditInstaller;

        public UnattendServiceControlToAuditInstanceConverter(ILogging loggingInstance, string deploymentCachePath)
        {
            log = loggingInstance;
            serviceControlInstaller = new UnattendServiceControlInstaller(loggingInstance, deploymentCachePath);
            auditInstaller = new UnattendAuditInstaller(loggingInstance, deploymentCachePath);
        }

        public Result Convert(ServiceControlInstance instance, Options options)
        {
            var result = ValidateLicense();
            if (!result.Succeeded)
            {
                return result;
            }

            result = ValidateUpgradeVersion(instance);
            if (!result.Succeeded)
            {
                return result;
            }

            result = ValidateUpgradeAction(instance);
            if (!result.Succeeded)
            {
                return result;
            }

            var auditDetails = CopyDetailsFrom(instance);
            options.ApplyTo(auditDetails);

            result = ValidateServiceAccount(options, auditDetails);
            if (!result.Succeeded)
            {
                return result;
            }

            log.Info("Deleting old instance binaries...");

            if (!serviceControlInstaller.Delete(instance.Name, false, false))
            {
                // TODO: Do we need to recover somehow?
                return Result.Failed($"Unable to complete removal of existing instance binaries: {instance.Name}");
            }

            // We are deliberately writing to the same folder(s) as the old instance
            bool PromptToProceed(PathInfo _) => true;

            log.Info("Installing new audit instance binaries...");

            if (!auditInstaller.Add(auditDetails, PromptToProceed))
            {
                return Result.Failed("Unable to create new audit instance");
            }

            return Result.Success;
        }

        Result ValidateUpgradeAction(ServiceControlInstance instance)
        {
            var recommendedUpgradeAction = instance.GetRequiredUpgradeAction(serviceControlInstaller.ZipInfo.Version);
            switch (recommendedUpgradeAction)
            {
                case RequiredUpgradeAction.Upgrade:                    
                    return Result.Failed("This instance cannot be converted to an Audit instance. Upgrade the instance instead", RequiredUpgradeAction.Upgrade);
                case RequiredUpgradeAction.ConvertToAudit:
                    return Result.Success;
                case RequiredUpgradeAction.SplitOutAudit:
                    return Result.Failed("This instance cannot be converted to an Audit instance. Split the instance instead", RequiredUpgradeAction.SplitOutAudit);
                default:
                    return Result.Failed("This instance cannot be converted to an Audit instance. This instance has no recommended upgrade action");
            }
        }

        Result ValidateUpgradeVersion(ServiceControlInstance instance)
        {
            var upgradeInfo = UpgradeControl.GetUpgradeInfoForTargetVersion(serviceControlInstaller.ZipInfo.Version, instance.Version);
            if (instance.Version < upgradeInfo.CurrentMinimumVersion)
            {
                return Result.Failed($"An interim upgrade to version {upgradeInfo.RecommendedUpgradeVersion} is required before upgrading to version {serviceControlInstaller.ZipInfo.Version}. Download available at https://github.com/Particular/ServiceControl/releases/tag/{upgradeInfo.RecommendedUpgradeVersion}");
            }

            return Result.Success;
        }

        Result ValidateServiceAccount(Options options, ServiceControlAuditNewInstance auditDetails)
        {
            var account = UserAccount.ParseAccountName(auditDetails.ServiceAccount);
            if (!account.CheckPassword(auditDetails.ServiceAccountPwd))
            {
                return Result.Failed(
                    string.IsNullOrWhiteSpace(options.ServiceAccountPassword) 
                        ? $"A password is required for the service account: {auditDetails.ServiceAccount}" 
                        : $"Incorrect password for {auditDetails.ServiceAccount}");
            }

            return Result.Success;
        }

        Result ValidateLicense()
        {
            var licenseCheck = auditInstaller.CheckLicenseIsValid();
            if (!licenseCheck.Valid)
            {
                return Result.Failed(licenseCheck.Message);
            }

            return Result.Success;
        }

        static ServiceControlAuditNewInstance CopyDetailsFrom(ServiceControlInstance source)
        {
            return new ServiceControlAuditNewInstance
            {
                AuditQueue = source.AuditQueue,
                AuditLogQueue = source.AuditLogQueue,
                ForwardAuditMessages = source.ForwardAuditMessages,
                AuditRetentionPeriod = source.AuditRetentionPeriod,
                TransportPackage = source.TransportPackage,
                ConnectionString = source.ConnectionString,
                InstallPath = source.InstallPath,
                DBPath = source.DBPath,
                LogPath = source.LogPath,
                HostName = source.HostName,
                Port = source.Port,
                DatabaseMaintenancePort = source.DatabaseMaintenancePort,
                Name = source.Name,
                ServiceAccount = source.Service.Account,
                // NOTE: This should always be blank
                ServiceAccountPwd = source.ServiceAccountPwd,
                DisplayName = source.Service.DisplayName,
                ServiceDescription = source.Service.Description
            };
        }


        public class Options
        {
            public string AddressOfMainInstance { get; set; }
            public string ServiceAccountPassword { get; set; }

            public void ApplyTo(ServiceControlAuditNewInstance instance)
            {
                instance.ServiceControlQueueAddress = AddressOfMainInstance;
                if (string.IsNullOrWhiteSpace(ServiceAccountPassword) == false)
                {
                    instance.ServiceAccountPwd = ServiceAccountPassword;
                }
            }
        }

        public class Result
        {
            public bool Succeeded { get; set; }
            public string FailureReason { get; set; }
            public RequiredUpgradeAction? RequiredUpgradeAction { get; set; }

            public static Result Success = new Result { Succeeded = true };

            public static Result Failed(string reason, RequiredUpgradeAction? requiredUpgradeAction = null)
                => new Result { FailureReason = reason, RequiredUpgradeAction = requiredUpgradeAction };
        }
    }
}