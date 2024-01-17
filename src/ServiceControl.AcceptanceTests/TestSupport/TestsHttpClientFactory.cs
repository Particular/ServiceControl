namespace ServiceControl.AcceptanceTests.TestSupport;

using System;
using System.Net.Http;

class TestsHttpClientFactory(Func<string, HttpClient> factory) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => factory(name);
}