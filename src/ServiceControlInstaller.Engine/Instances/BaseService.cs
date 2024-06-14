namespace ServiceControlInstaller.Engine.Instances
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Runtime;
    using System.ServiceProcess;
    using System.Threading;
    using Engine;
    using FileSystem;
    using NuGet.Versioning;
    using Services;
    using TimeoutException = System.ServiceProcess.TimeoutException;

    public abstract class BaseService : IServiceInstance
    {
        public string Description { get; set; }
        public IWindowsServiceController Service { get; set; }
        public string InstallPath => Path.GetDirectoryName(Service.ExePath);
        public string DisplayName { get; set; }
        public string Name => Service.ServiceName;
        public string ServiceAccount { get; set; }
        public string ServiceAccountPwd { get; set; }

        public TransportInfo TransportPackage { get; set; }

        public SemanticVersion Version
        {
            get
            {
                // Service Can be registered but file deleted!
                if (File.Exists(Service.ExePath))
                {
                    var fileVersion = FileVersionInfo.GetVersionInfo(Service.ExePath);

                    return SemanticVersion.Parse(fileVersion.ProductVersion);
                }

                return new SemanticVersion(0, 0, 0);
            }
        }

        protected string GetDescription()
        {
            try
            {
                return Service.Description;
            }
            catch
            {
                return null;
            }
        }

        public bool TryStopService()
        {
            Service.Refresh();
            if (Service.Status == ServiceControllerStatus.Stopped)
            {
                return true;
            }

            Service.Stop();

            try
            {
                Service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(60));

                var t = TaskHelpers.Run(() =>
                {
                    var workingPath = Path.GetDirectoryName(Service.ExePath);
                    while (!HasUnderlyingProcessExited() || FilesAreInUse(workingPath, "*.dll"))
                    {
                        Thread.Sleep(250);
                    }
                });

                return t.Wait(TimeSpan.FromSeconds(5));
            }
            catch (TimeoutException)
            {
                return false;
            }
        }

        public bool TryStartService()
        {
            Service.Refresh();
            if (Service.Status == ServiceControllerStatus.Running)
            {
                return true;
            }

            Service.Start();

            try
            {
                Service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(120));
            }
            catch (TimeoutException)
            {
                return false;
            }

            return true;
        }

        bool HasUnderlyingProcessExited()
        {
            if (Service.ExePath == null)
            {
                return true;
            }

            return !Process
                .GetProcesses()
                .Any(p =>
                {
                    try
                    {
                        return p.MainModule.FileName == Service.ExePath;
                    }
                    catch
                    {
                        return false;
                    }
                });
        }

        static bool FilesAreInUse(string path, string searchPattern, bool recursive = false)
        {
            var filePaths = Directory.GetFiles(path, searchPattern, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);

            return filePaths.Any(FileIsInUse);
        }

        static bool FileIsInUse(string path)
        {
            try
            {
                // Note: Doesn't work on exe files.
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    // Don't actually do anything
                    return false;
                }
            }
            catch (IOException)
            {
                // If file does not exist, it's clearly not in use anymore
                return File.Exists(path);
            }
        }

        public string BackupAppConfig()
        {
            var backupDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Particular", "ServiceControlInstaller", "ConfigBackups", FileUtils.SanitizeFolderName(Service.ServiceName));
            if (!Directory.Exists(backupDirectory))
            {
                Directory.CreateDirectory(backupDirectory);
            }

            var configFile = $"{Service.ExePath}.config";
            if (!File.Exists(configFile))
            {
                return null;
            }

            var destinationFile = Path.Combine(backupDirectory, $"{Guid.NewGuid():N}.config");
            File.Copy(configFile, destinationFile);
            return destinationFile;
        }

        public abstract void Reload();


        public void UpgradeFiles(string zipFilePath)
        {
            // Do NOT use a TEMP folder as that could be on another drive and moving that will require first copying files.
            var newPath = InstallPath + ".new";
            var oldPath = InstallPath + ".old";

            // Prepare
            FileUtils.DeleteDirectory(newPath, true, false);
            Prepare(zipFilePath, newPath);

            // Swap
            MoveCurrentToOld(newPath, oldPath);
            MoveNewToCurrent(newPath, oldPath);
            PurgeOld(oldPath);
        }

        void MoveCurrentToOld(string newPath, string oldPath)
        {
            try
            {
                Directory.Move(InstallPath, oldPath);
            }
            catch (Exception ex)
            {
                try
                {
                    FileUtils.DeleteDirectory(newPath, true, false);
                }
                catch (Exception ex2)
                {
                    throw new("Error while moving previous version. Is the instance still running? Cleanup of preparation folder failed.", ex2);
                }
                throw new("Error while moving previous version. Is the instance still running?", ex);
            }
        }

        void MoveNewToCurrent(string newPath, string oldPath)
        {
            try
            {
                Directory.Move(newPath, InstallPath);
            }
            catch (Exception ex)
            {
                try
                {
                    // Try restoring previous version
                    Directory.Move(oldPath, InstallPath);
                    throw new("Error while making new version active but successfully restored previous version.", ex);
                }
                catch (Exception ex2)
                {
                    throw new($"Error while making new version active and unsuccessful in restoring previous version.\n\nManually restore '{oldPath}' to '{InstallPath}' to repair instance.", ex2);
                }
            }
        }


        static void PurgeOld(string oldPath)
        {
            try
            {
                FileUtils.DeleteDirectory(oldPath, true, false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ServiceControlInstaller.Engine.Instances.BaseService::PurgeOld Unable to cleanup {oldPath}. Reason: {ex.Message} ({ex.GetType().FullName})");
                // Ignore, did our best. Unfortunately no context to report a warning
            }
        }

        protected abstract void Prepare(string zipFilePath, string destDir);
    }
}