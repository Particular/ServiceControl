# ServiceControl MCP server

ServiceControl can host a Model Context Protocol (MCP) server inside the main ServiceControl process.

## Enablement

Enable the read-only MCP server with the `ServiceControl/EnableMcpServer` setting.

```xml
<add key="ServiceControl/EnableMcpServer" value="true" />
```

If you also want write-capable retry tools, enable `ServiceControl/EnableMcpServerWriteMode` as well:

```xml
<add key="ServiceControl/EnableMcpServerWriteMode" value="true" />
```

When enabled, ServiceControl exposes MCP on `POST /mcp` using the same ASP.NET Core host, authentication, and authorization pipeline as the built-in API.

## Security

The MCP surface reuses the existing ServiceControl authentication and permission model:

- authentication is handled by the existing JWT/OIDC configuration
- authorization uses the same permission names as the HTTP API
- retry actions continue to flow through the existing audit trail

## Current tools

The first MCP tools focus on failures and recoverability groups:

- list failed messages
- get a failed message by id
- get the last attempt for a failed message
- get an errors summary
- list failed messages by endpoint
- retry a failed message
- list failure groups
- get failures in a failure group
- get retry history
- retry a failure group

## Deployment

Because the MCP server lives in the main ServiceControl host, it is deployed and versioned with the rest of ServiceControl.

For production, keep MCP disabled unless you need it. When enabled, protect the host with the same TLS, proxy, and authentication settings you would use for the HTTP API.

## Extending MCP

Add new tools under `src/ServiceControl/Mcp/Tools/` and reuse the existing ServiceControl services and permission policies. The current structure is intentionally small so future tools can be added without introducing a second security model or a separate host.