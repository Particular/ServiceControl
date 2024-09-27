﻿namespace ServiceControl.Transport.Tests
{
    using System;
    using System.Threading.Tasks;
    using ServiceControl.Transports.PostgreSql;
    using Transports;

    partial class TransportTestsConfiguration
    {
        public string ConnectionString { get; private set; }

        public ITransportCustomization TransportCustomization { get; private set; }

        public Task Configure()
        {
            TransportCustomization = new PostgreSqlTransportCustomization();
            ConnectionString = Environment.GetEnvironmentVariable(ConnectionStringKey) ?? @"User ID=user;Password=admin;Host=localhost;Port=54320;Database=sc-transport-tests;Pooling=true;Connection Lifetime=0;";

            if (string.IsNullOrEmpty(ConnectionString))
            {
                throw new Exception($"Environment variable {ConnectionStringKey} is required for PostgreSQL transport tests to run");
            }

            return Task.CompletedTask;
        }

        public Task Cleanup() => Task.CompletedTask;

        static string ConnectionStringKey = "PostgreSqlTransportConnectionString";
    }
}