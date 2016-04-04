namespace ServiceControlInstaller.Engine.Instances
{
    using System;
    using ServiceControlInstaller.Engine.Validation;

    public interface IServiceControlInstance : IContainPort, IContainInstancePaths, IContainTransportInfo, IServiceAccount
    {
        string Name { get; }
        string VirtualDirectory { get; }
        bool ForwardAuditMessages { get; }
        bool ForwardErrorMessages { get; }
        string HostName { get; }
        TimeSpan AuditRetentionPeriod { get; }
        TimeSpan ErrorRetentionPeriod { get; }
        Version Version  { get; }
    }
}
