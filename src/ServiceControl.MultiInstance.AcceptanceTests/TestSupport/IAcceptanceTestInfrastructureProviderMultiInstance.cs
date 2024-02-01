namespace ServiceControl.MultiInstance.AcceptanceTests.TestSupport
{
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Text.Json;

    interface IAcceptanceTestInfrastructureProviderMultiInstance
    {
        Dictionary<string, HttpClient> HttpClients { get; }

        Dictionary<string, JsonSerializerOptions> SerializerOptions { get; }

        Dictionary<string, dynamic> SettingsPerInstance { get; }
    }
}