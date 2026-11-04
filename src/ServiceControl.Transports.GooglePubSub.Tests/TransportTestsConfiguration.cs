namespace ServiceControl.Transport.Tests;

using System;
using System.Threading;
using System.Threading.Tasks;
using Transports;
using Transports.GooglePubSub;

class TransportTestsConfiguration
{
    public string ConnectionString { get; private set; }

    public ITransportCustomization TransportCustomization { get; private set; }

    public Task Configure(CancellationToken cancellationToken = default)
    {
        TransportCustomization = new GooglePubSubTransportCustomization();
        ConnectionString = Environment.GetEnvironmentVariable(ConnectionStringKey)
                           ?? Environment.GetEnvironmentVariable(ConnectionStringKey.ToUpperInvariant()); // Env keys are case sensitive, POSIX is all uppercase

        if (string.IsNullOrEmpty(ConnectionString))
        {
            throw new Exception($"Environment variable {ConnectionStringKey} is required for Google Pub/Sub transport tests to run");
        }

        return Task.CompletedTask;
    }

    public Task Cleanup(CancellationToken cancellationToken = default) => Task.CompletedTask;

    static readonly string ConnectionStringKey = "ServiceControl_TransportTests_GooglePubSub_ConnectionString";
}