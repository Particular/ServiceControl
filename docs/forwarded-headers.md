# Forwarded Headers Configuration

When ServiceControl instances are deployed behind a reverse proxy (like NGINX, Traefik, or a cloud load balancer) that terminates SSL/TLS, you need to configure forwarded headers so ServiceControl correctly understands the original client request.

## Configuration

ServiceControl instances can be configured via environment variables or App.config. Each instance type uses a different prefix.

### Environment Variables

| Instance | Prefix |
|----------|--------|
| ServiceControl (Primary) | `SERVICECONTROL_` |
| ServiceControl.Audit | `SERVICECONTROL_AUDIT_` |
| ServiceControl.Monitoring | `MONITORING_` |

| Setting | Default | Description |
|---------|---------|-------------|
| `{PREFIX}FORWARDEDHEADERS_ENABLED` | `true` | Enable forwarded headers processing |
| `{PREFIX}FORWARDEDHEADERS_TRUSTALLPROXIES` | `true` | Trust all proxies (auto-disabled if known proxies/networks set) |
| `{PREFIX}FORWARDEDHEADERS_KNOWNPROXIES` | (none) | Comma-separated IP addresses of trusted proxies |
| `{PREFIX}FORWARDEDHEADERS_KNOWNNETWORKS` | (none) | Comma-separated CIDR networks (e.g., `10.0.0.0/8,172.16.0.0/12`) |

### App.config

| Instance | Key Prefix |
|----------|------------|
| ServiceControl (Primary) | `ServiceControl/` |
| ServiceControl.Audit | `ServiceControl.Audit/` |
| ServiceControl.Monitoring | `Monitoring/` |

```xml
<appSettings>
  <add key="ServiceControl/ForwardedHeaders.Enabled" value="true" />
  <add key="ServiceControl/ForwardedHeaders.TrustAllProxies" value="false" />
  <add key="ServiceControl/ForwardedHeaders.KnownProxies" value="127.0.0.1,10.0.0.5" />
  <add key="ServiceControl/ForwardedHeaders.KnownNetworks" value="10.0.0.0/8,172.16.0.0/12" />
</appSettings>
```

## Examples

### Trust all proxies (default, suitable for containers)

```cmd
docker run -p 33333:33333 -e SERVICECONTROL_FORWARDEDHEADERS_ENABLED=true particular/servicecontrol:latest
```

### Restrict to specific proxies

```cmd
docker run -p 33333:33333 -e SERVICECONTROL_FORWARDEDHEADERS_ENABLED=true -e SERVICECONTROL_FORWARDEDHEADERS_KNOWNPROXIES=127.0.0.1,10.0.0.5 particular/servicecontrol:latest
```

### Restrict to specific networks

```cmd
docker run -p 33333:33333 -e SERVICECONTROL_FORWARDEDHEADERS_ENABLED=true -e SERVICECONTROL_FORWARDEDHEADERS_KNOWNNETWORKS=10.0.0.0/8,172.16.0.0/12 particular/servicecontrol:latest
```

When `KNOWNPROXIES` or `KNOWNNETWORKS` are set, `TRUSTALLPROXIES` is automatically disabled.

## What Headers Are Processed

When enabled, ServiceControl processes:

- `X-Forwarded-For` - Original client IP address
- `X-Forwarded-Proto` - Original protocol (http/https)
- `X-Forwarded-Host` - Original host header

## HTTP to HTTPS Redirect

When using a reverse proxy that terminates SSL, you can configure ServiceControl to redirect HTTP requests to HTTPS. This works in combination with forwarded headers:

1. The reverse proxy forwards both HTTP and HTTPS requests to ServiceControl
2. The proxy sets `X-Forwarded-Proto` to indicate the original protocol
3. ServiceControl reads this header (via forwarded headers processing)
4. If the original request was HTTP and redirect is enabled, ServiceControl returns a redirect to HTTPS

To enable HTTP to HTTPS redirect:

```cmd
set SERVICECONTROL_HTTPS_REDIRECTHTTPTOHTTPS=true
set SERVICECONTROL_HTTPS_PORT=443
```

Or in App.config:

```xml
<appSettings>
  <add key="ServiceControl/Https.RedirectHttpToHttps" value="true" />
  <add key="ServiceControl/Https.Port" value="443" />
</appSettings>
```

## Proxy Chain Behavior (ForwardLimit)

When processing `X-Forwarded-For` headers with multiple IPs (proxy chains), the behavior depends on trust configuration:

| Configuration | ForwardLimit | Behavior |
|---------------|--------------|----------|
| `TrustAllProxies = true` | `null` (no limit) | Processes all IPs, returns original client IP |
| `TrustAllProxies = false` | `1` (default) | Processes only the last proxy IP |

For example, with `X-Forwarded-For: 203.0.113.50, 10.0.0.1, 192.168.1.1`:

- **TrustAllProxies = true**: Returns `203.0.113.50` (original client)
- **TrustAllProxies = false**: Returns `192.168.1.1` (last proxy)

## Security Considerations

By default, `TrustAllProxies` is `true`, which is suitable for container deployments where the proxy is trusted infrastructure. For production deployments with untrusted networks, consider restricting to known proxies or networks to prevent header spoofing attacks.

### Forwarded Headers Behavior

When the proxy is trusted:

- `Request.Scheme` will be set from `X-Forwarded-Proto` (e.g., `https`)
- `Request.Host` will be set from `X-Forwarded-Host` (e.g., `servicecontrol.example.com`)
- Client IP will be available from `X-Forwarded-For`

When the proxy is **not** trusted (incorrect `KnownProxies`):

- `X-Forwarded-*` headers are **ignored** (not applied to the request)
- `Request.Scheme` remains `http`
- `Request.Host` remains the internal hostname
- The request is still processed (not blocked)

## See Also

- [Forwarded Headers Testing](forward-headers-testing.md) - Test forwarded headers configuration with curl
- [Reverse Proxy Testing](reverseproxy-testing.md) - Guide for testing with NGINX reverse proxy locally
