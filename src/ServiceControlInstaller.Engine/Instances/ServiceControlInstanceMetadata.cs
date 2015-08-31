// ReSharper disable MemberCanBePrivate.Global
namespace ServiceControlInstaller.Engine.Instances
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Security.AccessControl;
    using System.Security.Principal;
    using System.Xml.Serialization;
    using ServiceControlInstaller.Engine.Accounts;
    using ServiceControlInstaller.Engine.Configuration;
    using ServiceControlInstaller.Engine.FileSystem;
    using ServiceControlInstaller.Engine.Queues;
    using ServiceControlInstaller.Engine.ReportCard;
    using ServiceControlInstaller.Engine.Services;
    using ServiceControlInstaller.Engine.UrlAcl;
    using ServiceControlInstaller.Engine.Validation;

    public class ServiceControlInstanceMetadata : IServiceControlInstance
    {
        public string LogPath { get; set; }
        public string DBPath { get; set; }
        public string HostName { get; set; }
        public string InstallPath { get; set; }
        public int Port { get; set; }
        public string VirtualDirectory { get; set; }
        public string ErrorQueue { get; set; }
        public string ErrorLogQueue { get; set; }
        public string AuditQueue { get; set; }
        public string AuditLogQueue { get; set; }
        public bool ForwardAuditMessages { get; set; }
        public string TransportPackage { get; set; }
        public string ConnectionString { get; set; }
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string ServiceDescription { get; set; }

        [XmlIgnore]
        public string ServiceAccount { get; set; }
        [XmlIgnore]
        public string ServiceAccountPwd { get; set; }
        [XmlIgnore]
        public ReportCard ReportCard { get; set; }
        
        public string Url
        {
            get
            {
                //TODO: Introduce option for https?
                var baseUrl = string.Format("http://{0}:{1}/api/", HostName, Port);
                if (string.IsNullOrWhiteSpace(VirtualDirectory))
                {
                    return baseUrl;
                }
                return string.Format("{0}{1}{2}api/", baseUrl, VirtualDirectory, VirtualDirectory.EndsWith("/") ? "" : "/");
            }
        }

        public void CopyFiles(string zipFilePath)
        {
            var account = new NTAccount(UserAccount.ParseAccountName(ServiceAccount).QualifiedName);
            var readExecuteAccessRule = new FileSystemAccessRule(account, FileSystemRights.ReadAndExecute | FileSystemRights.Traverse | FileSystemRights.ListDirectory, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow);
            FileUtils.CreateDirectoryAndSetAcl(InstallPath, readExecuteAccessRule);

            var modifyAccessRule = new FileSystemAccessRule(account, FileSystemRights.Modify | FileSystemRights.Traverse | FileSystemRights.ListDirectory, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow);
            if (!string.IsNullOrWhiteSpace(LogPath))
            {
                FileUtils.CreateDirectoryAndSetAcl(LogPath, modifyAccessRule);
            }
            if (!string.IsNullOrWhiteSpace(LogPath))
            {
                FileUtils.CreateDirectoryAndSetAcl(DBPath, modifyAccessRule);
            }

            // Copy the binaries from a zip
            FileUtils.UnzipToSubdirectory(zipFilePath, InstallPath, "ServiceControl");
            FileUtils.UnzipToSubdirectory(zipFilePath, InstallPath, string.Format(@"Transports\{0}", TransportPackage));
        }

        public void WriteConfigurationFile()
        {
            var configuration = new ConfigurationWriter(this);
            configuration.Validate();
            configuration.Save();
        }

        public void RegisterService()
        {
            var serviceDetails = new WindowsServiceDetails
            {
                ServiceAccount = ServiceAccount,
                ServiceAccountPwd = ServiceAccountPwd,
                DisplayName = DisplayName,
                Name = Name,
                ImagePath = string.Format("\"{0}\" --serviceName={1}", Path.Combine(InstallPath, "ServiceControl.exe"), Name),
                ServiceDescription = ServiceDescription
            };
            var dependencies = new List<string>();
            if (TransportPackage.Equals("NServiceBus.MsmqTransport", StringComparison.OrdinalIgnoreCase))
            {
                dependencies.Add("MSMQ");
            }
            WindowsServiceController.RegisterNewService(serviceDetails, dependencies.ToArray());
        }

        public void RegisterUrlAcl()
        {
            var reservation = new UrlReservation(Url, new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null));
            reservation.Create();
        }

        public void RunInstanceToCreateQueues()
        {
            QueueCreation.RunQueueCreation(this);
        }

        public void Save(string path)
        {
            var serializer = new XmlSerializer(GetType());
            using (var stream = File.OpenWrite(path))
            {
                serializer.Serialize(stream, this);
            }
        }

        public static ServiceControlInstanceMetadata Load(string path)
        {
            var serializer = new XmlSerializer(typeof(ServiceControlInstanceMetadata));
            using (var stream = File.OpenRead(path))
            {
                return (ServiceControlInstanceMetadata)serializer.Deserialize(stream);
            }
        }

        public void Validate()
        {
            if (TransportPackage.Equals("MSMQ", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    MSMQConfigValidator.Validate();
                }
                catch(EngineValidationException ex)
                {
                    ReportCard.Errors.Add(ex.Message);

                }
            }

            try
            {
                PortValidator.Validate(this);
            }
            catch (EngineValidationException ex)
            {
                ReportCard.Errors.Add(ex.Message);
            }

            try
            {
                PathsValidator.Validate(this);
            }
            catch (EngineValidationException ex)
            {
                ReportCard.Errors.Add(ex.Message);
            }

            try
            {
                QueueNameValidator.Validate(this);
            }
            catch (EngineValidationException ex)
            {
                ReportCard.Errors.Add(ex.Message);
            }

            try
            {
                CheckForConflictingUrlAclReservations();
            }
            catch (EngineValidationException ex)
            {
                ReportCard.Errors.Add(ex.Message);
            }

            try
            {
                ServiceAccountValidation.Validate(this);
            }
            catch (IdentityNotMappedException)
            {
                ReportCard.Errors.Add("The service account specified does not exist");
            }
            catch (EngineValidationException ex)
            {
                ReportCard.Errors.Add(ex.Message);
            }
        }

        void CheckForConflictingUrlAclReservations()
        {
            foreach (var reservation in UrlReservation.GetAll().Where(p => p.Port == Port))
            {
                // exclusive or of reservation and instance - if only one of them has "localhost" then the UrlAcl will clash
                if ((reservation.HostName.Equals("localhost", StringComparison.OrdinalIgnoreCase) && !HostName.Equals("localhost", StringComparison.OrdinalIgnoreCase)) ||
                    (!reservation.HostName.Equals("localhost", StringComparison.OrdinalIgnoreCase) && HostName.Equals("localhost", StringComparison.OrdinalIgnoreCase)))
                {
                    throw new EngineValidationException(string.Format("Conflicting UrlAcls found - {0} vs {1}", Url, reservation.Url));
                }
            }
        }
    }
}