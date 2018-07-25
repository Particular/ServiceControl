namespace ServiceControl.LoadTests.AuditGenerator
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;

    class LoadGenerators
    {
        public LoadGenerators(Func<string, QueueInfo, CancellationToken, Task> generateMessages, int minLength, int maxLength)
        {
            this.generateMessages = generateMessages;
            this.minLength = minLength;
            this.maxLength = maxLength;
        }

        public Task ProcessedCountReported(string destination, long processed)
        {
            var gen = generators.GetOrAdd(destination, k => new LoadGenerator(k, generateMessages, minLength, maxLength));
            return gen.ProcessedCountReported(processed);
        }

        Func<string, QueueInfo, CancellationToken, Task> generateMessages;
        ConcurrentDictionary<string, LoadGenerator> generators = new ConcurrentDictionary<string, LoadGenerator>();
        int minLength;
        int maxLength;
    }
}