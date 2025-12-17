# HTTPS Configuration

ServiceControl instances can be configured to use HTTPS directly, enabling encrypted connections without relying on a reverse proxy for SSL termination.

## Configuration

ServiceControl instances can be configured via environment variables or App.config. Each instance type uses a different prefix.

### Environment Variables

| Instance                  | Prefix                  |
|---------------------------|-------------------------|
| ServiceControl (Primary)  | `SERVICECONTROL_`       |
| ServiceControl.Audit      | `SERVICECONTROL_AUDIT_` |
| ServiceControl.Monitoring | `MONITORING_`           |

| Setting                               | Default    | Description                                                    |
|---------------------------------------|------------|----------------------------------------------------------------|
| `{PREFIX}HTTPS_ENABLED`               | `false`    | Enable HTTPS with Kestrel                                      |
| `{PREFIX}HTTPS_CERTIFICATEPATH`       | (none)     | Path to the certificate file (.pfx)                            |
| `{PREFIX}HTTPS_CERTIFICATEPASSWORD`   | (none)     | Password for the certificate file                              |
| `{PREFIX}HTTPS_REDIRECTHTTPTOHTTPS`   | `false`    | Redirect HTTP requests to HTTPS                                |
| `{PREFIX}HTTPS_PORT`                  | (none)     | HTTPS port for redirect (required for reverse proxy scenarios) |
| `{PREFIX}HTTPS_ENABLEHSTS`            | `false`    | Enable HTTP Strict Transport Security                          |
| `{PREFIX}HTTPS_HSTSMAXAGESECONDS`     | `31536000` | HSTS max-age in seconds (default: 1 year)                      |
| `{PREFIX}HTTPS_HSTSINCLUDESUBDOMAINS` | `false`    | Include subdomains in HSTS policy                              |

### App.config

| Instance                  | Key Prefix              |
|---------------------------|-------------------------|
| ServiceControl (Primary)  | `ServiceControl/`       |
| ServiceControl.Audit      | `ServiceControl.Audit/` |
| ServiceControl.Monitoring | `Monitoring/`           |

```xml
<appSettings>
  <add key="ServiceControl/Https.Enabled" value="true" />
  <add key="ServiceControl/Https.CertificatePath" value="C:\certs\servicecontrol.pfx" />
  <add key="ServiceControl/Https.CertificatePassword" value="mycertpassword" />
  <add key="ServiceControl/Https.EnableHsts" value="true" />
</appSettings>
```

## Examples

### Direct HTTPS with certificate

```cmd
set SERVICECONTROL_HTTPS_ENABLED=true
set SERVICECONTROL_HTTPS_CERTIFICATEPATH=C:\certs\servicecontrol.pfx
set SERVICECONTROL_HTTPS_CERTIFICATEPASSWORD=mycertpassword
```

### Docker Example

```cmd
docker run -p 33333:33333 -e SERVICECONTROL_HTTPS_ENABLED=true -e SERVICECONTROL_HTTPS_CERTIFICATEPATH=/certs/servicecontrol.pfx -e SERVICECONTROL_HTTPS_CERTIFICATEPASSWORD=mycertpassword -v C:\certs:/certs:ro particular/servicecontrol:latest
```

### Reverse proxy with HTTP to HTTPS redirect

When using a reverse proxy that terminates SSL:

```cmd
set SERVICECONTROL_FORWARDEDHEADERS_ENABLED=true
set SERVICECONTROL_FORWARDEDHEADERS_TRUSTALLPROXIES=true
set SERVICECONTROL_HTTPS_REDIRECTHTTPTOHTTPS=true
set SERVICECONTROL_HTTPS_PORT=443
```

## Security Considerations

### Certificate Password Security

The certificate password is read as plain text from configuration. To minimize security risks:

#### Option 1: Use a certificate without a password (Recommended)

If the certificate file is protected with proper file system permissions, a password may not be necessary:

```bash
# Export certificate without password protection
openssl pkcs12 -in cert-with-password.pfx -out cert-no-password.pfx -nodes
```

Then restrict file access:

- **Windows:** Grant read access only to the service account running ServiceControl
- **Linux/Container:** Set file permissions to `400` (owner read only)

#### Option 2: Use platform secrets management

For container and cloud deployments, use the platform's secrets management instead of plain environment variables:

| Platform | Secrets Solution |
|----------|------------------|
| Kubernetes | [Kubernetes Secrets](https://kubernetes.io/docs/concepts/configuration/secret/) mounted as environment variables |
| Docker Swarm | [Docker Secrets](https://docs.docker.com/engine/swarm/secrets/) |
| Azure | [Azure Key Vault](https://learn.microsoft.com/en-us/azure/key-vault/) with managed identity |
| AWS | [AWS Secrets Manager](https://aws.amazon.com/secrets-manager/) or [SSM Parameter Store](https://docs.aws.amazon.com/systems-manager/latest/userguide/systems-manager-parameter-store.html) |

#### Option 3: Restrict file system access

If you must use a password-protected certificate:

- Never commit certificates or passwords to source control
- Restrict read access to the certificate file to only the ServiceControl service account
- Use environment variables rather than App.config (environment variables are not persisted to disk)
- Consider using Windows DPAPI or similar platform-specific encryption for config files

### Certificate File Security

- Store certificate files securely with appropriate file permissions
- Rotate certificates before expiration
- Use certificates from a trusted Certificate Authority for production
- Never commit certificate files to source control

### HSTS Considerations

- HSTS should not be tested on localhost because browsers cache the policy, which could break other local development
- HSTS is disabled in Development environment (ASP.NET Core excludes localhost by default)
- HSTS can be configured at either the reverse proxy level or in ServiceControl (but not both)
- HSTS is cached by browsers, so test carefully before enabling in production
- Start with a short max-age during initial deployment
- Consider the impact on subdomains before enabling `includeSubDomains`
- To test HSTS locally, use the [NGINX reverse proxy setup](reverseproxy-testing.md) with a custom hostname

### HTTP to HTTPS Redirect

The `HTTPS_REDIRECTHTTPTOHTTPS` setting is intended for use with a reverse proxy that handles both HTTP and HTTPS traffic. When enabled:

- The redirect uses HTTP 307 (Temporary Redirect) to preserve the request method
- The reverse proxy must forward both HTTP and HTTPS requests to ServiceControl
- ServiceControl will redirect HTTP requests to HTTPS based on the `X-Forwarded-Proto` header
- **Important:** You must also set `HTTPS_PORT` to specify the HTTPS port for the redirect URL

> **Note:** When running ServiceControl directly without a reverse proxy, the application only listens on a single protocol (HTTP or HTTPS). To test HTTP-to-HTTPS redirection locally, use the [NGINX reverse proxy setup](reverseproxy-testing.md).

## See Also

- [HTTPS Testing](https-testing.md) - Guide for testing HTTPS locally during development
- [Reverse Proxy Testing](reverseproxy-testing.md) - Testing with NGINX reverse proxy (HSTS, HTTP to HTTPS redirect)
- [Forwarded Headers Configuration](forwarded-headers.md) - Configure forwarded headers when behind a reverse proxy
