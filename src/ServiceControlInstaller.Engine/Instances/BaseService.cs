namespace ServiceControlInstaller.Engine.Instances
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.ServiceProcess;
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceControlInstaller.Engine.FileSystem;
    using ServiceControlInstaller.Engine.Services;
    using TimeoutException = System.ServiceProcess.TimeoutException;

    public abstract class BaseService : IServiceInstance
    {
        public string Description { get; set; }
        public string DisplayName { get; set; }
        public string Name => Service.ServiceName;
        public WindowsServiceController Service { get; set; }
        public string InstallPath => Path.GetDirectoryName(Service.ExePath);
        public string ServiceAccount { get; set; }
        public string ServiceAccountPwd { get; set; }

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
                var t = new Task(() =>
                {
                    while (!HasUnderlyingProcessExited())
                    {
                        Thread.Sleep(100);
                    }
                });
                t.Wait(5000);
            }
            catch (TimeoutException)
            {
                return false;
            }
            return true;
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
                Service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
            }
            catch (TimeoutException)
            {
                return false;
            }
            return true;
        }

        bool HasUnderlyingProcessExited()
        {
            try
            {
                if (Service.ExePath != null)
                {
                    var process = Process.GetProcesses().FirstOrDefault(p => p.MainModule.FileName == Service.ExePath);
                    return (process == null);
                }
            }
            catch
            {
                //Service isn't accessible 
            }
            return true;
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

        public Version Version
        {
            get
            {
                // Service Can be registered but file deleted!
                if (File.Exists(Service.ExePath))
                {
                    var fileVersion = FileVersionInfo.GetVersionInfo(Service.ExePath);
                    return new Version(fileVersion.FileMajorPart, fileVersion.FileMinorPart, fileVersion.FileBuildPart);
                }
                return new Version(0, 0, 0);
            }
        }

        public virtual void RefreshServiceProperties()
        {
            Service.Refresh();
        }
    }
}
