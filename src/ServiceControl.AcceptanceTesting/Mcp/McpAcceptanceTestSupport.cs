namespace ServiceControl.AcceptanceTesting.Mcp;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;

public static class McpAcceptanceTestSupport
{
    const string RequestedProtocolVersion = "2025-11-25";

    static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static async Task<HttpResponseMessage> InitializeMcpSession(HttpClient httpClient)
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
                    protocolVersion = RequestedProtocolVersion,
                    capabilities = new { },
                    clientInfo = new { name = "test-client", version = "1.0" }
                }
            })
        };
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));
        request.Headers.Add("MCP-Protocol-Version", RequestedProtocolVersion);
        return await httpClient.SendAsync(request);
    }

    public static async Task<McpSessionInfo> InitializeAndGetSessionInfo(HttpClient httpClient)
    {
        var response = await InitializeMcpSession(httpClient);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var initializeResponse = JsonSerializer.Deserialize<McpInitializeResponse>(await ReadMcpResponseJson(response), JsonOptions)!;
        var protocolVersion = initializeResponse.Result.ProtocolVersion;

        if (!response.Headers.TryGetValues("mcp-session-id", out var values))
        {
            return null;
        }

        var sessionId = values.FirstOrDefault();
        if (sessionId == null)
        {
            return null;
        }

        var initializedResponse = await SendInitializedNotification(httpClient, sessionId, protocolVersion);
        if (!initializedResponse.IsSuccessStatusCode)
        {
            return null;
        }

        return new McpSessionInfo(sessionId, protocolVersion);
    }

    public static async Task<HttpResponseMessage> SendMcpRequest(HttpClient httpClient, McpSessionInfo sessionInfo, string method, object @params)
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
        request.Headers.Add("mcp-session-id", sessionInfo.SessionId);
        request.Headers.Add("MCP-Protocol-Version", sessionInfo.ProtocolVersion);
        return await httpClient.SendAsync(request);
    }

    public static async Task<string> ReadMcpResponseJson(HttpResponseMessage response)
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

    public static McpListToolsResponse DeserializeListToolsResponse(string toolsJson) =>
        JsonSerializer.Deserialize<McpListToolsResponse>(toolsJson, JsonOptions)!;

    public static McpCallToolResponse DeserializeCallToolResponse(string toolResult) =>
        JsonSerializer.Deserialize<McpCallToolResponse>(toolResult, JsonOptions)!;

    public static void AssertToolsHaveOutputSchema(IEnumerable<JsonElement> tools)
    {
        foreach (var tool in tools)
        {
            Assert.That(tool.TryGetProperty("outputSchema", out var outputSchema), Is.True, $"Tool '{tool.GetProperty("name").GetString()}' should expose outputSchema.");
            Assert.That(outputSchema.ValueKind, Is.EqualTo(JsonValueKind.Object), $"Tool '{tool.GetProperty("name").GetString()}' should expose object outputSchema.");
        }
    }

    public static void AssertStructuredToolResponse(string rawResponse, JsonElement structuredContent, IReadOnlyList<McpContent> content, Action<JsonElement> assertStructuredContent)
    {
        Assert.That(structuredContent.ValueKind, Is.EqualTo(JsonValueKind.Object), rawResponse);
        assertStructuredContent(structuredContent);

        Assert.That(content, Has.Count.GreaterThanOrEqualTo(1), rawResponse);
        Assert.That(content[0].Type, Is.EqualTo("text"), rawResponse);
        Assert.That(content[0].Text, Is.Not.Null.And.Not.Empty, rawResponse);

        using var textPayload = JsonDocument.Parse(content[0].Text);
        Assert.That(JsonElement.DeepEquals(structuredContent, textPayload.RootElement), Is.True, $"text content should serialize the structured payload. Raw response: {rawResponse}");
    }

    static async Task<HttpResponseMessage> SendInitializedNotification(HttpClient httpClient, string sessionId, string protocolVersion)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = JsonContent.Create(new
            {
                jsonrpc = "2.0",
                method = "notifications/initialized"
            })
        };
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));
        request.Headers.Add("mcp-session-id", sessionId);
        request.Headers.Add("MCP-Protocol-Version", protocolVersion);
        return await httpClient.SendAsync(request);
    }
}

public record McpSessionInfo(string SessionId, string ProtocolVersion);

public class McpListToolsResponse
{
    public McpListToolsResult Result { get; set; }
}

public class McpListToolsResult
{
    public List<object> Tools { get; set; } = [];
}

public class McpCallToolResponse
{
    public McpCallToolResult Result { get; set; }
}

public class McpCallToolResult
{
    public JsonElement StructuredContent { get; set; }
    public List<McpContent> Content { get; set; } = [];
}

public class McpContent
{
    public string Type { get; set; }
    public string Text { get; set; }
}

class McpInitializeResponse
{
    public McpInitializeResult Result { get; set; }
}

class McpInitializeResult
{
    public string ProtocolVersion { get; set; }
}
