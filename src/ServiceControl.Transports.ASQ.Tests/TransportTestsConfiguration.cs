﻿namespace ServiceControl.Transport.Tests
{
    using System;
    using System.Threading.Tasks;
    using Transports;
    using Transports.ASQ;

    partial class TransportTestsConfiguration
    {
        public string ConnectionString { get; private set; }

        public ITransportCustomization TransportCustomization { get; private set; }

        public Task Configure()
        {
            TransportCustomization = new ASQTransportCustomization();
            ConnectionString = Environment.GetEnvironmentVariable(ConnectionStringKey);

            if (string.IsNullOrEmpty(ConnectionString))
            {
                throw new Exception($"Environment variable {ConnectionStringKey} is required for ASQ transport tests to run");
            }

            return Task.CompletedTask;
        }

        public Task Cleanup() => Task.CompletedTask;

        static string ConnectionStringKey = "ServiceControl.TransportTests.ASQ.ConnectionString";
    }
}