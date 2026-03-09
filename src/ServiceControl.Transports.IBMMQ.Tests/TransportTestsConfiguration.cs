namespace ServiceControl.Transport.Tests;

using System;
using System.Threading.Tasks;
using Transports;
using Transports.IBMMQ;
using NServiceBus;
using NServiceBus.Transport.IBMMQ;
using NUnit.Framework;

[SetUpFixture]
public class BootstrapFixture
{
    [OneTimeSetUp]
    public void RunBeforeAnyTests() => TransportTestFixture.QueueNameSeparator = '.';
}

class TransportTestsConfiguration
{
    public string ConnectionString { get; private set; }

    public ITransportCustomization TransportCustomization { get; private set; }

    public Task Configure()
    {
        TransportCustomization = new TestIBMMQTransportCustomization();
        ConnectionString = Environment.GetEnvironmentVariable(ConnectionStringKey);

        if (string.IsNullOrEmpty(ConnectionString))
        {
            throw new Exception($"Environment variable {ConnectionStringKey} is required for IBM MQ transport tests to run");
        }

        return Task.CompletedTask;
    }

    public Task Cleanup() => Task.CompletedTask;

    static string ConnectionStringKey = "ServiceControl_TransportTests_IBMMQ_ConnectionString";
}

sealed class TestIBMMQTransportCustomization : IBMMQTransportCustomization
{
    protected override IBMMQTransport CreateTransport(TransportSettings transportSettings, TransportTransactionMode preferredTransactionMode = TransportTransactionMode.ReceiveOnly)
    {
        transportSettings.Set<Action<IBMMQTransportOptions>>(o => o.ResourceNameSanitizer = name => name
                .Replace("ServiceControlMonitoring", "SCM") // Mitigate max queue name length
                .Replace("-", ".") // dash is an illegal char
        );
        return base.CreateTransport(transportSettings, preferredTransactionMode);
    }
}