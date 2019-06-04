namespace ServiceBus.Management.AcceptanceTests
{
    using System.Net.Http;
    using Newtonsoft.Json;
    using ServiceControl.Audit.Infrastructure;
    using ServiceControl.Audit.Infrastructure.Settings;

    interface IAcceptanceTestInfrastructureProvider
    {
        HttpClient HttpClient { get; }

        JsonSerializerSettings SerializerSettings { get; }

        Settings Settings { get; }
        OwinHttpMessageHandler Handler { get; }
        BusInstance Bus { get; }
    }
}