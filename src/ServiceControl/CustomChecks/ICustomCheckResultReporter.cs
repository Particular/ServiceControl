namespace ServiceControl.CustomChecks
{
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceControl.Contracts.CustomChecks;

    /// <summary>
    /// Where this host's own custom check results go. A host that owns its database stores them; a
    /// host on a dedicated audit database sends them to the primary, which is the only host
    /// ServicePulse asks.
    /// </summary>
    interface ICustomCheckResultReporter
    {
        Task Report(CustomCheckDetail detail, CancellationToken cancellationToken = default);
    }

    class LocalCustomCheckResultReporter(CustomCheckResultProcessor processor) : ICustomCheckResultReporter
    {
        public Task Report(CustomCheckDetail detail, CancellationToken cancellationToken = default) => processor.ProcessResult(detail, cancellationToken);
    }
}
