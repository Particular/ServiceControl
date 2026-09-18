#nullable enable
namespace ServiceControl.AcceptanceTesting.Mcp;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

public static class McpAcceptanceTestSupport
{
    static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNameCaseInsensitive = true };

    const string RequestedProtocolVersion = "2025-11-25";

    public static async Task<HttpResponseMessage> InitializeMcpSession(HttpClient httpClient, CancellationToken cancellationToken = default)
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
        return await httpClient.SendAsync(request, cancellationToken);
    }

    public static async Task<McpSessionInfo?> InitializeAndGetSessionInfo(HttpClient httpClient, CancellationToken cancellationToken = default)
    {
        var response = await InitializeMcpSession(httpClient, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var initializeResponse = JsonSerializer.Deserialize<McpInitializeResponse>(await ReadMcpResponseJson(response, cancellationToken), SerializerOptions);
        var protocolVersion = initializeResponse?.Result?.ProtocolVersion;
        if (string.IsNullOrEmpty(protocolVersion))
        {
            return null;
        }

        if (!response.Headers.TryGetValues("mcp-session-id", out var values))
        {
            return null;
        }

        var sessionId = values.FirstOrDefault();
        if (string.IsNullOrEmpty(sessionId))
        {
            return null;
        }

        var initializedResponse = await SendInitializedNotification(httpClient, sessionId, protocolVersion, cancellationToken);
        if (!initializedResponse.IsSuccessStatusCode)
        {
            return null;
        }

        return new McpSessionInfo(sessionId, protocolVersion);
    }

    public static async Task<HttpResponseMessage> SendMcpRequest(HttpClient httpClient, McpSessionInfo sessionInfo, string method, object @params, CancellationToken cancellationToken = default)
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
        return await httpClient.SendAsync(request, cancellationToken);
    }

    public static async Task<string> ReadMcpResponseJson(HttpResponseMessage response, CancellationToken cancellationToken = default)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var contentType = response.Content.Headers.ContentType?.MediaType;

        if (contentType == "text/event-stream")
        {
            foreach (var line in body.Split('\n'))
            {
                if (line.StartsWith("data: ", StringComparison.Ordinal))
                {
                    return line["data: ".Length..];
                }
            }
        }

        return body;
    }

    public static McpListToolsResponse DeserializeListToolsResponse(string toolsJson) =>
        JsonSerializer.Deserialize<McpListToolsResponse>(toolsJson, SerializerOptions)!;

    public static McpCallToolResponse DeserializeCallToolResponse(string toolResult) =>
        JsonSerializer.Deserialize<McpCallToolResponse>(toolResult, SerializerOptions)!;

    public static string FormatToolsForApproval(List<JsonElement> sortedTools) =>
        JsonSerializer.Serialize(sortedTools);

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

        using var textPayload = JsonDocument.Parse(content[0].Text!);
        Assert.That(JsonElement.DeepEquals(structuredContent, textPayload.RootElement), Is.True, $"text content should serialize the structured payload. Raw response: {rawResponse}");
    }

    static async Task<HttpResponseMessage> SendInitializedNotification(HttpClient httpClient, string sessionId, string protocolVersion, CancellationToken cancellationToken)
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
        return await httpClient.SendAsync(request, cancellationToken);
    }
}

public sealed record McpSessionInfo(string SessionId, string ProtocolVersion);

public sealed class McpListToolsResponse
{
    public McpListToolsResult Result { get; set; } = new();
}

public sealed class McpListToolsResult
{
    public List<JsonElement> Tools { get; set; } = [];
}

public sealed class McpCallToolResponse
{
    public McpCallToolResult Result { get; set; } = new();
}

public sealed class McpCallToolResult
{
    public JsonElement StructuredContent { get; set; }
    public List<McpContent> Content { get; set; } = [];
}

public sealed class McpContent
{
    public string Type { get; set; } = string.Empty;
    public string? Text { get; set; }
}

public sealed class McpInitializeResponse
{
    public McpInitializeResult Result { get; set; } = new();
}

public sealed class McpInitializeResult
{
    public string ProtocolVersion { get; set; } = string.Empty;
}