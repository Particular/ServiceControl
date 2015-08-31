namespace ServiceControlInstaller.CustomActions
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Microsoft.Deployment.WindowsInstaller;
    using ServiceControlInstaller.Engine.FileSystem;
    using ServiceControlInstaller.Engine.Instances;
    using ServiceControlInstaller.Engine.LicenseMgmt;
    using ServiceControlInstaller.Engine.Unattended;

    public class CustomActionsInstall
    {
        [CustomAction]
        public static ActionResult ServiceControlUnattendedInstall(Session session)
        {
            var logger = new MSILogger(session);

            var unattendedInstaller = new UnattendInstaller(logger, session["APPDIR"]);
            var zipInfo = ServiceControlZipInfo.Find(session["APPDIR"] ?? ".");
            
            if (!zipInfo.Present)
            {
               logger.Error("Zip file not found. Service Control service instances can not be upgraded or installed");
               return ActionResult.Failure;
            }

            PlatformInstallActions(session, zipInfo, logger, unattendedInstaller);
            UpgradeInstances(session, zipInfo, logger, unattendedInstaller);
            UnattendedInstall(session, logger, unattendedInstaller);
            ImportLicenseInstall(session, logger);
            return ActionResult.Success;
        }

        static void PlatformInstallActions(Session session, ServiceControlZipInfo zipInfo, MSILogger logger, UnattendInstaller unattendedInstaller)
        {
            var platformInstallerPropertyValue = session["PLATFORMINSTALLER"];
            bool platformInstallerFlag;
            bool.TryParse(platformInstallerPropertyValue, out platformInstallerFlag);

            if (!platformInstallerFlag)
                return;

            logger.Info("Platform Installer Flag Detected");
            //Add or Upgrade "Particular.ServiceControl" Service
            var defaultSCInstance = ServiceControlInstance.Instances().FirstOrDefault(p => string.Equals(p.Name, "Particular.ServiceControl", StringComparison.OrdinalIgnoreCase));
            if (defaultSCInstance != null)
            {
                if ((zipInfo.Present) && (zipInfo.Version > defaultSCInstance.Version))
                {
                    if (!unattendedInstaller.Upgrade(defaultSCInstance))
                    {
                        logger.Warn(string.Format("Failed to upgrade {0} to {1}", defaultSCInstance.Name, zipInfo.Version));
                    }
                }
            }
            else
            {
                var defaultInstanceDetails = new ServiceControlInstanceMetadata
                {
                    Name = "Particular.ServiceControl",
                    DisplayName = "Particular ServiceControl",
                    ServiceDescription = "Particular Software ServiceControl for NServiceBus",
                    TransportPackage = "MSMQ",
                    HostName = "localhost",
                    ForwardAuditMessages = false,
                    InstallPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Particular Software\ServiceControl"),
                    Port = 33333
                };
                try
                {
                    unattendedInstaller.Add(defaultInstanceDetails);
                }
                catch(Exception ex)
                {
                    logger.Error(string.Format("Failed to add instance - {0}", ex.Message));
                }
            }
        }

        static void UpgradeInstances(Session session, ServiceControlZipInfo zipInfo, MSILogger logger, UnattendInstaller unattendedInstaller)
        {
            var upgradeInstancesPropertyValue = session["UPGRADEINSTANCES"];
            if (string.IsNullOrWhiteSpace(upgradeInstancesPropertyValue))
                return;
            upgradeInstancesPropertyValue = upgradeInstancesPropertyValue.Trim();

            //determine what to upgrade
            var instancesToUpgrade = new List<ServiceControlInstance>();
            if (upgradeInstancesPropertyValue.Equals("*", StringComparison.OrdinalIgnoreCase) || upgradeInstancesPropertyValue.Equals("ALL", StringComparison.OrdinalIgnoreCase))
            {
                instancesToUpgrade.AddRange(ServiceControlInstance.Instances());
            }
            else
            {
                var candidates = upgradeInstancesPropertyValue.Replace(" ", "").Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                instancesToUpgrade.AddRange(ServiceControlInstance.Instances().Where(instance => candidates.Contains(instance.Name, StringComparer.OrdinalIgnoreCase)));
            }

            // do upgrades
            foreach (var instance in instancesToUpgrade)
            {
                if (zipInfo.Version > instance.Version)
                {
                    if (!unattendedInstaller.Upgrade(instance))
                    {
                        logger.Warn(string.Format("Failed to upgrade {0} to {1}", instance.Name, zipInfo.Version));
                    }
                }
            }
        }

        static void UnattendedInstall(Session session, MSILogger logger, UnattendInstaller unattendedInstaller)
        {
            logger.Info("Checking for unattended file");

            var unattendedFilePropertyValue = session["UNATTENDEDFILE"];
            if (string.IsNullOrWhiteSpace(unattendedFilePropertyValue))
                return;

            var serviceAccount = session["SERVICEACCOUNT"];
            var password = session["PASSWORD"];
            logger.Info(string.Format("UNATTENDEDFILE: {0}", unattendedFilePropertyValue));
            var currentDirectory = session["CURRENTDIRECTORY"];
            var unattendedFilePath = Environment.ExpandEnvironmentVariables(Path.IsPathRooted(unattendedFilePropertyValue) ? unattendedFilePropertyValue : Path.Combine(currentDirectory, unattendedFilePropertyValue));

            logger.Info(string.Format("Expanded unattended filepath to : {0}", unattendedFilePropertyValue));

            if (File.Exists(unattendedFilePath))
            {
                logger.Info(string.Format("File Exists : {0}", unattendedFilePropertyValue));
                var instanceToInstallDetails = ServiceControlInstanceMetadata.Load(unattendedFilePath);
                instanceToInstallDetails.ServiceAccount = serviceAccount;
                instanceToInstallDetails.ServiceAccountPwd = password;
                unattendedInstaller.Add(instanceToInstallDetails);
            }
            else
            {
                logger.Error(string.Format("The specified unattended install file was not found : '{0}'", unattendedFilePath));
            }
        }

        static void ImportLicenseInstall(Session session, MSILogger logger)
        {
            logger.Info("Checking for license file");

            var licenseFilePropertyValue = session["LICENSEFILE"];
            if (string.IsNullOrWhiteSpace(licenseFilePropertyValue))
                return;

            logger.Info(string.Format("LICENSEFILE: {0}", licenseFilePropertyValue));
            var currentDirectory = session["CURRENTDIRECTORY"];
            var licenseFilePath = Environment.ExpandEnvironmentVariables(Path.IsPathRooted(licenseFilePropertyValue) ? licenseFilePropertyValue : Path.Combine(currentDirectory, licenseFilePropertyValue));

            logger.Info(string.Format("Expanded license filepath to : {0}", licenseFilePropertyValue));

            if (File.Exists(licenseFilePath))
            {
                logger.Info(string.Format("File Exists : {0}", licenseFilePropertyValue));
                string errormessage;
                if (!LicenseManager.TryImportLicense(licenseFilePath, out errormessage))
                {
                    logger.Error(errormessage);
                }
            }
            else
            {
                logger.Error(string.Format("The specified license install file was not found : '{0}'", licenseFilePath));
            }
        }

    }
}
