namespace ServiceBus.Management.AcceptanceTests
{
    using System.Net.Http;
    using Infrastructure;
    using Infrastructure.Settings;
    using Newtonsoft.Json;

    interface IAcceptanceTestInfrastructureProvider
    {
        HttpClient HttpClient { get; }

        JsonSerializerSettings SerializerSettings { get; }

        Settings Settings { get; }
        OwinHttpMessageHandler Handler { get; }
        BusInstance Bus { get; }
    }
}