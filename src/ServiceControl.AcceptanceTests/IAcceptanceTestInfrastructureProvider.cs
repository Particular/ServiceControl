namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using Microsoft.AspNet.SignalR.Client.Http;

    public interface IAcceptanceTestInfrastructureProvider
    {
        Dictionary<string, ServiceControlInstanceReference> Instances { get; }
    }

    public class ServiceControlInstanceReference : IDisposable
    {

        public ServiceControlInstanceReference(HttpClient httpClient, IHttpClient signalRClient, string id)
        {
            HttpClient = httpClient;
            SignalRClient = signalRClient;
            Id = id;
        }
        public string Id { get; }
        public HttpClient HttpClient { get; }
        public IHttpClient SignalRClient { get; }

        public void Dispose()
        {
            HttpClient?.Dispose();
        }
    }
}