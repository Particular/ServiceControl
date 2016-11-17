namespace NServiceBus.AcceptanceTesting
{
    using System;
    using System.Collections.Concurrent;

    public abstract class ScenarioContext
    {
        private ConcurrentQueue<string> traceMessages = new ConcurrentQueue<string>();

        public bool EndpointsStarted { get; set; }
        public string Exceptions { get; set; }
        public string SessionId { get; set; }
        public bool HasNativePubSubSupport { get; set; }

        public string Trace => string.Join(Environment.NewLine, traceMessages);

        public void AddTrace(string trace)
        {
            traceMessages.Enqueue(trace);
        }
    }
}