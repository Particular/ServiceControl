namespace ServiceControl.Infrastructure.Api;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Api;
using ServiceControl.Api.Contracts;

// Manual retention-sweep API. The persister's IRetentionSweeper is resolved *optionally* so the
// same controller/route is mapped on every persister: EFCore registers it and gets 202/409/200;
// RavenDB registers nothing (its retention is the server-side @expires bundle) and gets 501.
class RetentionApi(IServiceProvider serviceProvider, Settings settings) : IRetentionApi
{
    const string NotSupportedReason = "The current storage has no retention sweeper.";

    public Task<RetentionSweepResponse> SweepAsync(RetentionSweepRequest request, CancellationToken cancellationToken = default)
    {
        // Resolve the sweeper lazily and optionally — never required via constructor injection, or
        // a RavenDB-backed instance would throw at resolve time. Absent => 501 Not Implemented.
        var sweeper = serviceProvider.GetService<ServiceControl.Persistence.IRetentionSweeper>();
        if (sweeper is null)
        {
            return Task.FromResult(NotSupported());
        }

        // Maintenance mode refuses mutating operations; a sweep while the DB is being maintained
        // would contend with the maintenance work.
        if (settings.PersisterSpecificSettings?.MaintenanceMode == true)
        {
            return Task.FromResult(new RetentionSweepResponse { Status = "maintenance", Reason = "The instance is in maintenance mode." });
        }

        request ??= new RetentionSweepRequest();

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
            ServiceControl.Persistence.ManualSweepOutcome.Started => new RetentionSweepResponse
            {
                Status = "started",
                StartedAt = attempt.StartedAt,
                ErrorCutoff = attempt.ErrorCutoff,
                EventsCutoff = attempt.EventsCutoff
            },
            ServiceControl.Persistence.ManualSweepOutcome.AlreadyRunning => new RetentionSweepResponse
            {
                Status = "already-running",
                StartedAt = attempt.StartedAt
            },
            _ => new RetentionSweepResponse
            {
                Status = "already-running",
                StartedAt = attempt.StartedAt
            }
        });
    }

    public Task<RetentionSweepStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var sweeper = serviceProvider.GetService<ServiceControl.Persistence.IRetentionSweeper>();
        if (sweeper is null)
        {
            return Task.FromResult(new RetentionSweepStatus { Reason = NotSupportedReason });
        }

        // Map the persister's status record onto the API contract DTO (the two share a name but
        // live in different namespaces: ServiceControl.Persistence vs ServiceControl.Api.Contracts).
        ServiceControl.Persistence.RetentionSweepStatus status = sweeper.GetStatus();

        return Task.FromResult(new RetentionSweepStatus
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

    static RetentionSweepResponse NotSupported() => new()
    {
        Status = "not-supported",
        Reason = NotSupportedReason
    };

    static RetentionSweepResponse Invalid(string reason) => new()
    {
        Status = "invalid-cutoff",
        Reason = reason
    };
}