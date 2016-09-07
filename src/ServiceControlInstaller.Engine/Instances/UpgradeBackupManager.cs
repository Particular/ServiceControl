// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace ServiceControlInstaller.Engine.Instances
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using ServiceControlInstaller.Engine.Configuration;
    using ServiceControlInstaller.Engine.FileSystem;

    public class UpgradeBackupManager
    {
        public class BackupMessage
        {
            public string Message { get; set; }
            public DateTime TimeStamp { get; set; }
            public string Severity { get; set; }
        }

        public List<BackupMessage> Messages = new List<BackupMessage>();

        private ServiceControlInstance instance;
        private string zipFilePath;
        private string targetFolder;
        public string BackupExePath { get; set; }

        public UpgradeBackupManager(ServiceControlInstance instance, string zipFilePath, string targetFolder)
        {
            this.instance = instance;
            this.zipFilePath = zipFilePath;
            this.targetFolder = targetFolder;
         
        }

        public void EnterBackupMode()
        {
            if (instance.Version < SettingsList.MaintenanceMode.SupportedFrom)
            {
                instance.TryStopService();
                BackupExePath = Path.GetTempFileName();
                File.Copy(Path.Combine(instance.InstallPath, "ServiceControl.exe"), BackupExePath, true );
                FileUtils.UnzipToSubdirectory(zipFilePath, instance.InstallPath, "LegacyBackup");
                instance.TryStartService();
            }
            else
            {
                if (!instance.InMaintenanceMode)
                {
                    instance.TryStopService();
                    instance.EnableMaintenanceMode();
                }
                instance.TryStartService();
            }
        }

        public void ExitBackupMode()
        {
            if (instance.Version < SettingsList.MaintenanceMode.SupportedFrom)
            {
                File.Copy(BackupExePath, Path.Combine(instance.InstallPath, "ServiceControl.exe"), true);
            }
        }

        IEnumerable<string> GetDatabaseNames()
        {
            var response = Get("databases");
            using (var responseStream = response.GetResponseStream())
            {
                if (responseStream == null)
                    yield break;

                using (var streamReader = new StreamReader(responseStream))
                {
                    var results = JArray.Parse(streamReader.ReadToEnd());
                    foreach (var result in results)
                    {
                        yield return result.Value<string>();
                    }
                }
            }
        }

        HttpWebResponse Get(string relativePath)
        {
            var url = instance.StorageUrl + relativePath;
            var httpWebRequest = (HttpWebRequest) WebRequest.Create(url);
            httpWebRequest.Credentials = CredentialCache.DefaultNetworkCredentials;
            httpWebRequest.Method = WebRequestMethods.Http.Get;
            return (HttpWebResponse) httpWebRequest.GetResponse();
        }

        JObject GetJson(string relativePath)
        {
            var response = Get(relativePath);
            using (var responseStream = response.GetResponseStream())
            {
                if (responseStream == null)
                    return null;

                using (var streamReader = new StreamReader(responseStream))
                {
                    return JObject.Parse(streamReader.ReadToEnd());
                }
            }
        }

        HttpWebResponse PostJson(string relativePath, JObject jObject)
        {
            var url = instance.StorageUrl + relativePath;

            var httpWebRequest = (HttpWebRequest) WebRequest.Create(url);
            httpWebRequest.Credentials = CredentialCache.DefaultNetworkCredentials;
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.Method = WebRequestMethods.Http.Post;
            using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
            {
                streamWriter.Write(jObject);
                streamWriter.Flush();
                streamWriter.Close();
            }
            return (HttpWebResponse) httpWebRequest.GetResponse();
        }

        public bool BackupDatabase()
        {
            var result = false;

            //old SC use SystemDB not ServiceControl
            var prefix = GetDatabaseNames().Contains("ServiceControl") ? "databases/ServiceControl/" : "";

            var backupRequest = JObject.FromObject(new
            {
                BackupLocation = targetFolder.Replace("\\", "\\\\")
            });

            var response = PostJson(prefix + "admin/backup", backupRequest);
            if (response.StatusCode == HttpStatusCode.Created)
            {
                var messageCount = 0;
                bool running;
                do
                {
                    var json = GetJson(prefix + "docs/Raven/Backup/Status");
                    running = json["IsRunning"].Value<bool>();

                    /* Dump Messages to the console */
                    var jsonMsgs = json["Messages"].Children().ToList();
                    foreach (var jsonMsg in jsonMsgs.Skip(messageCount))
                    {
                        var backupMessage = JsonConvert.DeserializeObject<BackupMessage>(jsonMsg.ToString());
                        Messages.Add(backupMessage);
                        if (backupMessage.Severity == "Error")
                        {
                            instance.ReportCard.Errors.Add(backupMessage.Message);
                        }
                    }
                    messageCount = Messages.Count;


                    if (!running)
                    {
                        result = Messages.All(p => p.Severity != "Error");
                    }

                } while (running);
            }
            return result;
        }
    }
}
