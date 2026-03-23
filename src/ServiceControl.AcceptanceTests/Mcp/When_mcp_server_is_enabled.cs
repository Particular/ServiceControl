namespace ServiceControl.AcceptanceTests.Mcp;

using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using AcceptanceTesting;
using NServiceBus.AcceptanceTesting;
using NUnit.Framework;
using Particular.Approvals;

[TestFixture]
class When_mcp_server_is_enabled : AcceptanceTest
{
    [SetUp]
    public void EnableMcp() => SetSettings = s => s.EnableMcpServer = true;

    [Test]
    public async Task Should_expose_mcp_endpoint()
    {
        await Define<ScenarioContext>()
            .Done(async _ =>
            {
                var response = await InitializeMcpSession();
                return response.StatusCode == HttpStatusCode.OK;
            })
            .Run();
    }

    [Test]
    public async Task Should_list_primary_instance_tools()
    {
        string toolsJson = null;

        await Define<ScenarioContext>()
            .Done(async _ =>
            {
                var sessionId = await InitializeAndGetSessionId();
                if (sessionId == null)
                {
                    return false;
                }

                var response = await SendMcpRequest(sessionId, "tools/list", new { });
                if (response == null)
                {
                    return false;
                }

                toolsJson = await ReadMcpResponseJson(response);
                return response.StatusCode == HttpStatusCode.OK;
            })
            .Run();

        Assert.That(toolsJson, Is.Not.Null);
        var mcpResponse = JsonSerializer.Deserialize<McpListToolsResponse>(toolsJson, JsonOptions)!;
        var sortedTools = mcpResponse.Result.Tools.Cast<JsonElement>().OrderBy(t => t.GetProperty("name").GetString()).ToList();
        var formattedTools = JsonSerializer.Serialize(sortedTools, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Approver.Verify(formattedTools);
    }

    static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    class McpListToolsResponse
    {
        public McpListToolsResult Result { get; set; }
    }

    class McpListToolsResult
    {
        public List<object> Tools { get; set; } = [];
    }

    async Task<HttpResponseMessage> InitializeMcpSession()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = JsonContent.Create(new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "initialize",
                @params = new
                {
                    protocolVersion = "2025-03-26",
                    capabilities = new { },
                    clientInfo = new { name = "test-client", version = "1.0" }
                }
            })
        };
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));
        return await HttpClient.SendAsync(request);
    }

    async Task<string> InitializeAndGetSessionId()
    {
        var response = await InitializeMcpSession();
        if (response.StatusCode != HttpStatusCode.OK)
        {
            return null;
        }

        if (response.Headers.TryGetValues("mcp-session-id", out var values))
        {
            return values.FirstOrDefault();
        }

        return null;
    }

    static async Task<string> ReadMcpResponseJson(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        var contentType = response.Content.Headers.ContentType?.MediaType;

        if (contentType == "text/event-stream")
        {
            foreach (var line in body.Split('\n'))
            {
                if (line.StartsWith("data: "))
                {
                    return line.Substring("data: ".Length);
                }
            }
        }

        return body;
    }

    async Task<HttpResponseMessage> SendMcpRequest(string sessionId, string method, object @params)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = JsonContent.Create(new
            {
                jsonrpc = "2.0",
                id = 2,
                method,
                @params
            })
        };
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));
        request.Headers.Add("mcp-session-id", sessionId);
        return await HttpClient.SendAsync(request);
    }
}
