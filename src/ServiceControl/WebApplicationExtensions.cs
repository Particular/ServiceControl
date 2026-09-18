namespace ServiceControl;

using Infrastructure.WebApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.AspNetCore;
using ServiceControl.Hosting.ForwardedHeaders;
using ServiceControl.Hosting.Https;
using ServiceControl.Hosting.QueryTimeout;
using ServiceControl.Hosting.RequestId;
using ServiceControl.Infrastructure;
using ServiceControl.Infrastructure.Health;

public static class WebApplicationExtensions
{
    public static void UseServiceControl(this WebApplication app, ForwardedHeadersSettings forwardedHeadersSettings, HttpsSettings httpsSettings, bool enableMcpServer = false)
    {
        app.UseRequestIdHeader();
        app.UseQueryTimeoutResponse();
        app.UseServiceControlForwardedHeaders(forwardedHeadersSettings);
        app.UseServiceControlHttps(httpsSettings);
        app.UseResponseCompression();
        app.UseMiddleware<BodyUrlRouteFix>();
        app.UseHttpLogging();
        app.UseCors();
        app.MapControllers();

        if (enableMcpServer)
        {
            app.MapGet(global::ServiceControl.Mcp.McpServerConfiguration.Route, () => Results.Content("""
                <html>
                  <head>
                    <title>405: Method Not Allowed</title>
                  </head>
                  <body>
                    <h1>405: Method Not Allowed</h1>
                    <p>
                      This is an MCP server endpoint and cannot be accessed directly via a
                      browser or unsupported transports like SSE. Please use a streamable HTTP
                      transport.
                    </p>
                  </body>
                </html>
                """, "text/html", System.Text.Encoding.UTF8, StatusCodes.Status405MethodNotAllowed)).RequireCors(global::ServiceControl.Mcp.McpServerConfiguration.CorsPolicyName);
            app.MapMcp(global::ServiceControl.Mcp.McpServerConfiguration.Route).RequireCors(global::ServiceControl.Mcp.McpServerConfiguration.CorsPolicyName);
        }

        app.MapServiceControlHealthChecks();
    }
}