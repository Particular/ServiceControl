namespace ServiceControl.Api;

using System.Threading;
using System.Threading.Tasks;
using Contracts;

/// <summary>
/// Manual retention-sweep API. The implementation resolves the persister's sweeper
/// optionally: when no sweeper is registered (e.g. RavenDB, which uses server-side document
/// expiration) the operations report that the feature is not supported rather than silently
/// no-op'ing.
/// </summary>
public interface IRetentionApi
{
    /// <summary>
    /// Starts a manual retention sweep with caller-supplied cutoffs. The delete work runs in
    /// the background on a host-lifetime token; this method returns as soon as the run is
    /// accepted (or refused because one is already running / unsupported).
    /// </summary>
    Task<RetentionSweepResponse> SweepAsync(RetentionSweepRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a point-in-time snapshot of sweep execution state for polling.
    /// </summary>
    Task<RetentionSweepStatus> GetStatusAsync(CancellationToken cancellationToken = default);
}