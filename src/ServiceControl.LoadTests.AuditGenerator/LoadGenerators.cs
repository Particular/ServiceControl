namespace ServiceControl.LoadTests.AuditGenerator
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;

    class LoadGenerators
    {
        Func<string, CancellationToken, Task> generateMessages;
        ConcurrentDictionary<string, LoadGenerator> generators = new ConcurrentDictionary<string, LoadGenerator>();
        int minLength;
        int maxLength;

        public LoadGenerators(Func<string, CancellationToken, Task> generateMessages, int minLength, int maxLength)
        {
            this.generateMessages = generateMessages;
            this.minLength = minLength;
            this.maxLength = maxLength;
        }

        public Task QueueLengthReported(string queue, string machine, int length)
        {
            var destination = $"{queue}@{machine}";
            var gen = generators.GetOrAdd(destination, k => new LoadGenerator(k, generateMessages, minLength, maxLength));
            return gen.QueueLengthReported(length);
        }
    }
}