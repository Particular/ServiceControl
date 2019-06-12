namespace ServiceControlInstaller.Engine
{
    using System;
    using Instances;

    public interface ILogging
    {
        void Info(string message);
        void Warn(string message);
        void Error(string message);
    }

    public interface ITransportConfig
    {
        TransportInfo TransportPackage { get; }
        string ErrorQueue { get; }
        string ConnectionString { get; }
    }

    public interface IServiceControlTransportConfig : ITransportConfig, IServiceInstance
    {
        string AuditQueue { get; }
        string ErrorLogQueue { get; }
        string AuditLogQueue { get; }
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
        Version Version { get; }
    }

    public interface IURLInfo
    {
        string Url { get; }
        string BrowsableUrl { get; }
    }

    public interface IServiceInstance : IServiceAccount, IVersionInfo
    {
        string Name { get; }
        string DisplayName { get; }
    }

    public interface IInstallable
    {
        bool SkipQueueCreation { get; }
    }

    public interface IMonitoringInstance : IServiceInstance, IServicePaths, ITransportConfig, IHttpInstance, IURLInfo, IInstallable
    {
    }

    public interface IDatabaseMaintenanceSupport : IVersionInfo
    {
        int? DatabaseMaintenancePort { get; }
    }

    public interface IServiceControlAuditInstance : IServiceInstance, IServiceControlPaths, IServiceControlTransportConfig, IHttpInstance, IInstallable, IDatabaseMaintenanceSupport
    {
        string VirtualDirectory { get; }
        bool ForwardAuditMessages { get; }
        TimeSpan AuditRetentionPeriod { get; }
    }

    public interface IServiceControlInstance : IServiceInstance, IServiceControlPaths, IServiceControlTransportConfig, IHttpInstance, IURLInfo, IInstallable, IDatabaseMaintenanceSupport
    {
        string VirtualDirectory { get; }
        bool ForwardErrorMessages { get; }
        TimeSpan ErrorRetentionPeriod { get; }
        bool IsUpdatingDataStore { get; set; }
    }
}