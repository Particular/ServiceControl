namespace ServiceControl.Infrastructure.Api;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Persistence;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Api;
using ServiceControl.Api.Contracts;
using RetentionSweepStatus = Persistence.RetentionSweepStatus;

// Manual retention-purge API. The persister's IRetentionSweeper is resolved *optionally* so the
// same controller/route is mapped on every persister: EFCore registers it and gets 202/409/200;
// RavenDB registers nothing (its retention is the server-side @expires bundle) and gets 501.
class RetentionApi(IServiceProvider serviceProvider, Settings settings) : IRetentionApi
{
    public const string NotSupportedReason = "The currently configured storage has no retention sweeper.";
    public const string StatusMaintenance = "maintenance";
    public const string StatusStarted = "started";
    public const string StatusNotSupported = "not-supported";
    public const string StatusAlreadyRunning = "already-running";
    public const string StatusInvalidCutoff = "invalid-cutoff";


    public Task<RetentionPurgeResponse> Sweep(RetentionPurgeRequest request, CancellationToken cancellationToken = default)
    {
        // Resolve the sweeper lazily and optionally — never required via constructor injection, or
        // a RavenDB-backed instance would throw at resolve time. Absent => 501 Not Implemented.
        var sweeper = serviceProvider.GetService<IRetentionSweeper>();
        if (sweeper is null)
        {
            return Task.FromResult(NotSupported());
        }

        // Maintenance mode refuses mutating operations; a sweep while the DB is being maintained
        // would contend with the maintenance work.
        if (settings.PersisterSpecificSettings?.MaintenanceMode == true)
        {
            return Task.FromResult(new RetentionPurgeResponse { Status = StatusMaintenance, Reason = "The instance is in maintenance mode." });
        }

        request ??= new RetentionPurgeRequest();

        // Cutoffs must be UTC and in the past. A future cutoff would delete nothing and is almost
        // certainly a caller mistake, so it is rejected rather than clamped.
        if (TryValidateCutoff(request.ErrorCutoff, out var errorCutoff, out var error) is false)
        {
            return Task.FromResult(Invalid(error));
        }

        if (TryValidateCutoff(request.EventsCutoff, out var eventsCutoff, out error) is false)
        {
            return Task.FromResult(Invalid(error));
        }

        var attempt = sweeper.TryStartManualSweep(errorCutoff, eventsCutoff, cancellationToken);

        return Task.FromResult(attempt.Outcome switch
        {
            RetentionSweepStatus.Started => new RetentionPurgeResponse { Status = StatusStarted, StartedAt = attempt.StartedAt, ErrorCutoff = attempt.ErrorCutoff, EventsCutoff = attempt.EventsCutoff },
            RetentionSweepStatus.AlreadyRunning => new RetentionPurgeResponse { Status = StatusAlreadyRunning, StartedAt = attempt.StartedAt },
            _ => new RetentionPurgeResponse { Status = StatusAlreadyRunning, StartedAt = attempt.StartedAt }
        });
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
            LastEventsCutoff = status.LastEventsCutoff,
            LastError = status.LastError
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

        if (value.Kind != DateTimeKind.Utc)
        {
            validated = null;
            error = "Cutoffs must be specified as UTC DateTime values.";
            return false;
        }

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

    static RetentionPurgeResponse NotSupported() => new() { Status = StatusNotSupported, Reason = NotSupportedReason };

    static RetentionPurgeResponse Invalid(string reason) => new() { Status = StatusInvalidCutoff, Reason = reason };
}