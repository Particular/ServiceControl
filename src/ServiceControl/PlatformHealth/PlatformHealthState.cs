namespace ServiceControl.PlatformHealth
{
    using System;
    using System.Collections.Concurrent;
    using System.Linq;
    using ServiceControl.Api.Contracts;
    using ServiceControl.Contracts.CustomChecks;

    public class PlatformHealthState
    {
        internal void Record(CustomCheckDetail detail)
        {
            if (!InternalCustomCheckClassification.IsInternal(detail.CustomCheckId))
            {
                return;
            }

            var id = detail.GetDeterministicId();
            var report = new CheckState
            {
                Id = id,
                CheckId = detail.CustomCheckId,
                Category = detail.Category,
                HasFailed = detail.HasFailed,
                Message = detail.FailureReason,
                ReportedAt = detail.ReportedAt,
                InstanceName = detail.OriginatingEndpoint.Name,
                Host = detail.OriginatingEndpoint.Host,
                HostId = detail.OriginatingEndpoint.HostId
            };

            checks.AddOrUpdate(id, report, (_, previous) => report.ReportedAt >= previous.ReportedAt ? report : previous);
        }

        internal CheckState[] GetChecks() => checks.Values
            .OrderBy(check => check.InstanceName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(check => check.CheckId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(check => check.Id)
            .ToArray();

        public PlatformHealthView GetHealth() => GetHealth(GetChecks());

        internal static PlatformHealthView GetHealth(CheckState[] currentChecks)
        {
            var failedChecks = currentChecks
                .Where(check => check.HasFailed)
                .ToArray();

            return new PlatformHealthView
            {
                Status = currentChecks.Length == 0 ? "unknown" : failedChecks.Length == 0 ? "healthy" : "unhealthy",
                Severity = currentChecks.Length == 0 ? "unknown" : failedChecks.Length == 0 ? "none" : "error",
                Alerts = failedChecks.Select(check => new PlatformHealthAlert
                {
                    Id = check.Id,
                    CheckId = check.CheckId,
                    Category = check.Category,
                    Message = check.Message,
                    ReportedAt = check.ReportedAt,
                    InstanceName = check.InstanceName,
                    Host = check.Host,
                    HostId = check.HostId
                }).ToArray()
            };
        }

        readonly ConcurrentDictionary<Guid, CheckState> checks = new();

        internal sealed record CheckState
        {
            public Guid Id { get; init; }
            public string CheckId { get; init; }
            public string Category { get; init; }
            public bool HasFailed { get; init; }
            public string Message { get; init; }
            public DateTime ReportedAt { get; init; }
            public string InstanceName { get; init; }
            public string Host { get; init; }
            public Guid HostId { get; init; }
        }
    }
}