namespace ServiceControl.AcceptanceTests.Licensing
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceControl.Transports.BrokerThroughput;

    class FakeBrokerThroughputQuery(params (string QueueName, long Throughput)[] queues) : IBrokerThroughputQuery
    {
        public Dictionary<string, string> Data { get; } = new() { ["Version"] = "1.2.3" };

        public string MessageTransport => "FakeBroker";

        public string ScopeType => null;

        public KeyDescriptionPair[] Settings => [new KeyDescriptionPair(SettingKey, "Where the fake broker lives")];

        public void Initialize(ReadOnlyDictionary<string, string> settings) { }

        public bool HasInitialisationErrors(out string errorMessage)
        {
            errorMessage = string.Empty;
            return false;
        }

        public Task<(bool Success, List<string> Errors, string Diagnostics)> TestConnection(CancellationToken cancellationToken = default) =>
            Task.FromResult((true, new List<string>(), "Fake broker reachable"));

        public async IAsyncEnumerable<IBrokerQueue> GetQueueNames([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var (queueName, _) in queues)
            {
                yield return new FakeBrokerQueue(queueName, SanitizeEndpointName(queueName));
            }

            await Task.CompletedTask;
        }

        public async IAsyncEnumerable<QueueThroughput> GetThroughputPerDay(IBrokerQueue brokerQueue, DateOnly startDate, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var (_, throughput) in queues.Where(queue => queue.QueueName == brokerQueue.QueueName))
            {
                // Yesterday, because a usage report only counts complete days.
                yield return new QueueThroughput
                {
                    DateUTC = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
                    TotalThroughput = throughput
                };
            }

            await Task.CompletedTask;
        }

        public string SanitizeEndpointName(string endpointName) => endpointName.Replace('/', '.');

        public string SanitizedEndpointNameCleanser(string endpointName) => endpointName;

        public const string SettingKey = "FakeBroker/ConnectionString";

        class FakeBrokerQueue(string queueName, string sanitizedName) : IBrokerQueue
        {
            public string QueueName { get; } = queueName;

            public string SanitizedName { get; } = sanitizedName;

            public string Scope => null;

            public List<string> EndpointIndicators { get; } = [];
        }
    }
}
