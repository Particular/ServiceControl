namespace ServiceControlInstaller.Engine
{
    using System;
    using System.Collections.Generic;
    using Configuration.ServiceControl;
    using Instances;
    using NuGet.Versioning;

    public interface ILogging
    {
        void Info(string message);
        void Warn(string message);
        void Error(string message);
    }

    public interface ITransportConfig
    {
        TransportInfo TransportPackage { get; }
        string ConnectionString { get; }
    }

    public interface IPersistenceConfig
    {
        PersistenceManifest PersistenceManifest { get; }
    }

    public interface IHttpInstance
    {
        int Port { get; }
        string HostName { get; }
    }

    public interface IServicePaths
    {
        string InstallPath { get; }
        string LogPath { get; }
    }

    public interface IServiceControlPaths : IServicePaths
    {
        string DBPath { get; }
    }

    public interface IServiceAccount
    {
        string ServiceAccount { get; }
        string ServiceAccountPwd { get; }
    }

    public interface IVersionInfo
    {
        SemanticVersion Version { get; }
    }

    public interface IURLInfo
    {
        string Url { get; }
        string BrowsableUrl { get; }
    }

    public interface IServiceInstance : IServiceAccount, IVersionInfo
    {
        string Name { get; }

        string InstanceName { get; }

        string DisplayName { get; }
    }

    public interface IInstallable
    {
        bool SkipQueueCreation { get; }
    }

    public interface IMonitoringInstance : IServiceInstance, IServicePaths, ITransportConfig, IHttpInstance, IURLInfo, IInstallable
    {
        string ErrorQueue { get; }
    }

    public interface IDatabaseMaintenanceSupport : IVersionInfo
    {
        int? DatabaseMaintenancePort { get; }
    }

    public interface IServiceControlBaseInstance : IHttpInstance,
        IDatabaseMaintenanceSupport,
        IServiceInstance,
        IServiceControlPaths,
        IInstallable,
        ITransportConfig,
        IPersistenceConfig
    {
        bool EnableFullTextSearchOnBodies { get; }
    }

    public interface IServiceControlAuditInstance : IServiceControlBaseInstance
    {
        string AuditQueue { get; }
        string AuditLogQueue { get; }
        string VirtualDirectory { get; }
        bool ForwardAuditMessages { get; }
        TimeSpan AuditRetentionPeriod { get; }
        string ServiceControlQueueAddress { get; set; }
    }

    public interface IServiceControlInstance : IServiceControlBaseInstance, IURLInfo
    {
        string ErrorQueue { get; }
        string ErrorLogQueue { get; }
        string VirtualDirectory { get; }
        bool ForwardErrorMessages { get; }
        TimeSpan ErrorRetentionPeriod { get; }
        TimeSpan? AuditRetentionPeriod { get; set; }
        List<RemoteInstanceSetting> RemoteInstances { get; }
    }
}