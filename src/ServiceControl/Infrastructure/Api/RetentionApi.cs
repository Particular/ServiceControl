namespace ServiceControl.Infrastructure.Api;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Persistence;
using ServiceControl.Api;
using ServiceControl.Api.Contracts;
using RetentionSweepStatus = Persistence.RetentionSweepStatus;

// Manual retention-purge API. The persister's IRetentionSweeper is resolved *optionally* so the
// same controller/route is mapped on every persister: EFCore registers it and gets 202/409/200;
// RavenDB registers nothing (its retention is the server-side @expires bundle) and gets 501.
class RetentionApi(IServiceProvider serviceProvider) : IRetentionApi
{
    public const string NotSupportedReason = "The currently configured storage has no retention sweeper.";


    public Task<RetentionPurgeResponse> Sweep(RetentionPurgeRequest request, CancellationToken cancellationToken = default)
    {
        // Resolve the sweeper lazily and optionally — never required via constructor injection, or
        // a RavenDB-backed instance would throw at resolve time. Absent => 501 Not Implemented.
        var sweeper = serviceProvider.GetService<IRetentionSweeper>();
        if (sweeper is null)
        {
            return Task.FromResult((RetentionPurgeResponse)new() { Status = RetentionPurgeStatus.NotSupported, Reason = NotSupportedReason });
        }


        request ??= new RetentionPurgeRequest();

        // Cutoffs must be UTC and in the past. A future cutoff would delete everything and is almost
        // certainly a caller mistake, so it is rejected rather than clamped.
        if (!TryValidateCutoff(request.ErrorCutoff, out var errorCutoff, out var error)
            || !TryValidateCutoff(request.EventsCutoff, out var eventsCutoff, out error))
        {
            return Task.FromResult((RetentionPurgeResponse)new() { Status = RetentionPurgeStatus.Error, Reason = error });
        }

        var attempt = sweeper.TryStartManualSweep(errorCutoff, eventsCutoff, cancellationToken);

        return Task.FromResult(attempt.Outcome == RetentionSweepStatus.Started
            ? new RetentionPurgeResponse { Status = RetentionPurgeStatus.Started, StartedAt = attempt.StartedAt, ErrorCutoff = attempt.ErrorCutoff, EventsCutoff = attempt.EventsCutoff }
            : new RetentionPurgeResponse { Status = RetentionPurgeStatus.AlreadyRunning, StartedAt = attempt.StartedAt });
    }

    public Task<RetentionPurgeStatusResponse> GetStatus(CancellationToken cancellationToken = default)
    {
        var sweeper = serviceProvider.GetService<IRetentionSweeper>();
        if (sweeper is null)
        {
            return Task.FromResult(new RetentionPurgeStatusResponse { Reason = NotSupportedReason });
        }

        // Map the persister's status record onto the API contract DTO (the two share a name but
        // live in different namespaces: ServiceControl.Persistence vs ServiceControl.Api.Contracts).
        var status = sweeper.GetStatus();

        return Task.FromResult(new RetentionPurgeStatusResponse
        {
            IsRunning = status.IsRunning,
            LastStartedAt = status.LastStartedAt,
            LastFinishedAt = status.LastFinishedAt,
            LastErrorCutoff = status.LastErrorCutoff,
            LastEventsCutoff = status.LastEventsCutoff
        });
    }

    static bool TryValidateCutoff(DateTime? supplied, out DateTime? validated, out string error)
    {
        if (supplied is null)
        {
            validated = null;
            error = null;
            return true;
        }

        var value = supplied.Value;

        if (value.Kind == DateTimeKind.Unspecified)
        {
            validated = null;
            error = "Cutoffs must be specified as UTC DateTime values.";
            return false;
        }

        value = value.ToUniversalTime();

        if (value > DateTime.UtcNow)
        {
            validated = null;
            error = "Cutoffs must not be in the future.";
            return false;
        }

        validated = value;
        error = null;
        return true;
    }
}