namespace ServiceControl.SagaAudit
{
    using System;
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus.CustomChecks;

    class SagaAuditDestinationCustomCheck : CustomCheck
    {
        readonly State stateHolder;
        static readonly TimeSpan retentionTime = TimeSpan.FromHours(24);

        public SagaAuditDestinationCustomCheck(State stateHolder, RavenDBPersisterSettings settings)
            : base("Saga Audit Destination", "Health", settings.OverrideCustomCheckRepeatTime ?? TimeSpan.FromMinutes(15))
        {
            this.stateHolder = stateHolder;
        }

        public override Task<CheckResult> PerformCheck()
        {
            var failedEndpoints = stateHolder.GetFailedEndpoints();

            if (failedEndpoints.Length == 0)
            {
                return passResult;
            }

            var message = $"In the last 24 hours, the following endpoints have reported saga audit data to the ServiceControl Primary instance. Instead, saga audit data should be sent to the Audit Queue Name configured in the ServiceControl Audit Instance. Affected endpoints: "
                + string.Join(", ", failedEndpoints);

            return Task.FromResult(CheckResult.Failed(message));
        }

        static Task<CheckResult> passResult = Task.FromResult(CheckResult.Pass);

        public class State
        {
            readonly ConcurrentDictionary<string, DateTime> failedEndpoints = new ConcurrentDictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

            public void Fail(string endpointName)
            {
                var now = DateTime.UtcNow;
                var cutoff = now - retentionTime;

                failedEndpoints[endpointName] = DateTime.UtcNow;

                foreach (var key in failedEndpoints.Keys)
                {
                    if (failedEndpoints.TryGetValue(key, out var time) && time < cutoff)
                    {
                        failedEndpoints.TryRemove(key, out _);
                    }
                }
            }

            public string[] GetFailedEndpoints()
            {
                var cutoff = DateTime.UtcNow - retentionTime;
                return failedEndpoints
                    .Where(pair => pair.Value > cutoff)
                    .Select(pair => pair.Key)
                    .OrderBy(name => name)
                    .ToArray();
            }
        }
    }
}