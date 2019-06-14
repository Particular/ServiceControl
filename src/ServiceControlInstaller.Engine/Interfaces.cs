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
        string ConnectionString { get; }
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
        string ErrorQueue { get; }
    }

    public interface IDatabaseMaintenanceSupport : IVersionInfo
    {
        int? DatabaseMaintenancePort { get; }
    }

    public interface IServiceControlAuditInstance : IServiceInstance, IServiceControlPaths, IHttpInstance, IInstallable, IDatabaseMaintenanceSupport, ITransportConfig
    {
        string AuditQueue { get; }
        string AuditLogQueue { get; }
        string VirtualDirectory { get; }
        bool ForwardAuditMessages { get; }
        TimeSpan AuditRetentionPeriod { get; }
    }

    public interface IServiceControlInstance : IServiceInstance, IServiceControlPaths, IHttpInstance, IURLInfo, IInstallable, IDatabaseMaintenanceSupport, ITransportConfig
    {
        string ErrorQueue { get; }
        string ErrorLogQueue { get; }
        string VirtualDirectory { get; }
        bool ForwardErrorMessages { get; }
        TimeSpan ErrorRetentionPeriod { get; }
        TimeSpan AuditRetentionPeriod { get; set; }
    }
}