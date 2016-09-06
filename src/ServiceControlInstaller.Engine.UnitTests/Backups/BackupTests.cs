namespace ServiceControlInstaller.Engine.UnitTests.Backups
{
    using System;
    using System.IO;
    using NUnit.Framework;
    using ServiceControlInstaller.Engine.FileSystem;
    using ServiceControlInstaller.Engine.Instances;

    public class BackupTests
    {
        [Test, Explicit]
        public void StartBackup()
        {

            var zipFile = ServiceControlZipInfo.Find(@"..\..\..\..\Zip").FilePath;
            foreach (var i in ServiceControlInstance.Instances())
            {
               var target = $@"c:\foo-{i.Port}";
               if (Directory.Exists(target))
                    Directory.Delete(target, true);

               var x = new UpgradeBackupManager(i,zipFile, target);
               x.EnterBackupMode();
               if (!x.BackupDatabase())
               {
                    Console.WriteLine($"Failed to backup {i.Name} database to {target}");
               }
            }
        }
    }
}
