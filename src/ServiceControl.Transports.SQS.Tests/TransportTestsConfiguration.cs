namespace ServiceControl.Transport.Tests
{
    using System;
    using System.Threading.Tasks;
    using ServiceControl.Infrastructure;
    using Transports;
    using Transports.SQS;

    partial class TransportTestsConfiguration
    {
        public string ConnectionString { get; private set; }

        public ITransportCustomization TransportCustomization { get; private set; }

        public Task Configure()
        {
            TransportCustomization = new SQSTransportCustomization(LoggerUtil.CreateStaticLogger<SQSTransportCustomization>());
            ConnectionString = Environment.GetEnvironmentVariable(ConnectionStringKey);

            if (string.IsNullOrEmpty(ConnectionString))
            {
                throw new Exception($"Environment variable {ConnectionStringKey} is required for SQS transport tests to run");
            }

            return Task.CompletedTask;
        }

        public Task Cleanup() => Task.CompletedTask;

        static string ConnectionStringKey = "ServiceControl_TransportTests_SQS_ConnectionString";
    }
}