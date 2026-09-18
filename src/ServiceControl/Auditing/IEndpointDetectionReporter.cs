namespace ServiceControl.Auditing
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceControl.Operations;

    /// <summary>
    /// Where the endpoints detected from audit messages go besides this host's known endpoints. On a
    /// shared database that is nowhere, the known endpoints table is the primary's own. On a
    /// dedicated audit database they are reported to the primary.
    /// </summary>
    interface IEndpointDetectionReporter
    {
        Task Report(IReadOnlyCollection<EndpointDetails> endpoints, CancellationToken cancellationToken = default);
    }

    class NoEndpointDetectionReporter : IEndpointDetectionReporter
    {
        public Task Report(IReadOnlyCollection<EndpointDetails> endpoints, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
