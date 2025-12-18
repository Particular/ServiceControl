# ServiceControl Production Hosting Guide

This guide covers hosting and security configuration for ServiceControl in production environments. All scenarios assume HTTPS and authentication are required.

---

## Configuration Basics

### Instance Types and Prefixes

ServiceControl consists of three deployable instances:

| Instance                  | Purpose                              | Config Prefix           | Env Var Prefix          | Default Port |
|---------------------------|--------------------------------------|-------------------------|-------------------------|--------------|
| ServiceControl (Primary)  | Error handling, retries, heartbeats  | `ServiceControl/`       | `SERVICECONTROL_`       | 33333        |
| ServiceControl.Audit      | Audit message ingestion and querying | `ServiceControl.Audit/` | `SERVICECONTROL_AUDIT_` | 44444        |
| ServiceControl.Monitoring | Endpoint performance monitoring      | `Monitoring/`           | `MONITORING_`           | 33633        |

### Configuration Methods

Settings can be configured via:

- **App.config** - Recommended for Windows service deployments
- **Environment variables** - Recommended for containers

### Host and Port Configuration

Configure the hostname and port that each instance listens on:

**App.config:**

```xml
<!-- ServiceControl Primary -->
<appSettings>
  <add key="ServiceControl/Hostname" value="localhost" />
  <add key="ServiceControl/Port" value="33333" />
</appSettings>

<!-- ServiceControl.Audit -->
<appSettings>
  <add key="ServiceControl.Audit/Hostname" value="localhost" />
  <add key="ServiceControl.Audit/Port" value="44444" />
</appSettings>

<!-- ServiceControl.Monitoring -->
<appSettings>
  <add key="Monitoring/Hostname" value="localhost" />
  <add key="Monitoring/Port" value="33633" />
</appSettings>
```

**Environment variables:**

```cmd
rem ServiceControl Primary
set SERVICECONTROL_HOSTNAME=localhost
set SERVICECONTROL_PORT=33333

rem ServiceControl.Audit
set SERVICECONTROL_AUDIT_HOSTNAME=localhost
set SERVICECONTROL_AUDIT_PORT=44444

rem ServiceControl.Monitoring
set MONITORING_HOSTNAME=localhost
set MONITORING_PORT=33633
```

> **Note:** Use `localhost` or `+` (all interfaces) for the hostname. When behind a reverse proxy, use `localhost` and configure the proxy to forward to the appropriate port.

### Remote Instances Configuration

The Primary instance must be configured to communicate with Audit instances for scatter-gather operations (aggregating data across instances):

**App.config:**

```xml
<!-- ServiceControl Primary - configure remote ServiceControl and/or Audit instance(s) -->
<appSettings>
  <add key="ServiceControl/RemoteInstances" value="[{&quot;api_uri&quot;:&quot;https://servicecontrol-audit:44444/api&quot;}]" />
</appSettings>
```

**Environment variables:**

```cmd
rem Single Audit instance
set SERVICECONTROL_REMOTEINSTANCES=[{"api_uri":"https://servicecontrol-audit:44444/api"}]
```

For multiple Audit instances:

**App.config:**

```xml
<add key="ServiceControl/RemoteInstances" value="[{&quot;api_uri&quot;:&quot;https://servicecontrol-audit1:44444/api&quot;},{&quot;api_uri&quot;:&quot;https://servicecontrol-audit2:44444/api&quot;}]" />
```

**Environment variables:**

```cmd
set SERVICECONTROL_REMOTEINSTANCES=[{"api_uri":"https://servicecontrol-audit1:44444/api"},{"api_uri":"https://servicecontrol-audit2:44444/api"}]
```

> **Important:** When authentication is enabled, all instances (Primary, Audit, Monitoring) must use the **same** Identity Provider (IdP) Authority and Audience settings. Client tokens are forwarded to remote instances during scatter-gather operations.

---

## Production Deployment Scenarios

### Scenario 1: Reverse Proxy with Authentication

A reverse proxy (NGINX, IIS, cloud load balancer) handles SSL/TLS termination, and ServiceControl validates JWT tokens.

**Architecture:**

```text
Client → HTTPS → Reverse Proxy → HTTP → ServiceControl
                 (SSL termination)     (JWT validation)
```

**Security Features:**

| Feature                 | Status                          |
|-------------------------|---------------------------------|
| JWT Authentication      | ✅ Enabled                       |
| Kestrel HTTPS           | ❌ Disabled (handled by proxy)   |
| HTTPS Redirection       | ✅ Enabled (optional)            |
| HSTS                    | ❌ Disabled (configure at proxy) |
| Restricted CORS Origins | ✅ Enabled                       |
| Forwarded Headers       | ✅ Enabled                       |
| Restricted Proxy Trust  | ✅ Enabled                       |

> **Note:** HTTPS redirection is optional in this scenario. The reverse proxy typically handles HTTP to HTTPS redirection at its layer. However, enabling it at ServiceControl provides defense-in-depth - if an HTTP request somehow bypasses the proxy and reaches ServiceControl directly, it will be redirected to the HTTPS URL. This requires configuring `Https.Port` to specify the external HTTPS port used by the proxy.

#### ServiceControl Primary Configuration

**App.config:**

```xml
<appSettings>
  <!-- Host and Port -->
  <add key="ServiceControl/Hostname" value="localhost" />
  <add key="ServiceControl/Port" value="33333" />

  <!-- Remote Audit Instance(s) -->
  <add key="ServiceControl/RemoteInstances" value="[{&quot;api_uri&quot;:&quot;https://servicecontrol-audit/api&quot;}]" />

  <!-- Authentication -->
  <add key="ServiceControl/Authentication.Enabled" value="true" />
  <add key="ServiceControl/Authentication.Authority" value="https://login.microsoftonline.com/{tenant-id}/v2.0" />
  <add key="ServiceControl/Authentication.Audience" value="api://servicecontrol" />

  <!-- ServicePulse client configuration (Primary instance only) -->
  <add key="ServiceControl/Authentication.ServicePulse.ClientId" value="{servicepulse-client-id}" />
  <add key="ServiceControl/Authentication.ServicePulse.Authority" value="https://login.microsoftonline.com/{tenant-id}/v2.0" />
  <add key="ServiceControl/Authentication.ServicePulse.ApiScopes" value="[&quot;api://servicecontrol/access_as_user&quot;]" />

  <!-- Forwarded headers - trust only your reverse proxy -->
  <add key="ServiceControl/ForwardedHeaders.Enabled" value="true" />
  <add key="ServiceControl/ForwardedHeaders.TrustAllProxies" value="false" />
  <add key="ServiceControl/ForwardedHeaders.KnownProxies" value="10.0.0.5" />
  <!-- Or use CIDR notation: -->
  <!-- <add key="ServiceControl/ForwardedHeaders.KnownNetworks" value="10.0.0.0/24" /> -->

  <!-- HTTP to HTTPS redirect (optional - can also be handled by proxy) -->
  <add key="ServiceControl/Https.RedirectHttpToHttps" value="true" />
  <add key="ServiceControl/Https.Port" value="443" />

  <!-- Restrict CORS to your ServicePulse domain -->
  <add key="ServiceControl/Cors.AllowedOrigins" value="https://servicepulse" />
</appSettings>
```

**Environment variables:**

```cmd
rem Host and Port
set SERVICECONTROL_HOSTNAME=localhost
set SERVICECONTROL_PORT=33333

rem Remote Audit Instance(s)
set SERVICECONTROL_REMOTEINSTANCES=[{"api_uri":"https://servicecontrol-audit/api"}]

rem Authentication
set SERVICECONTROL_AUTHENTICATION_ENABLED=true
set SERVICECONTROL_AUTHENTICATION_AUTHORITY=https://login.microsoftonline.com/{tenant-id}/v2.0
set SERVICECONTROL_AUTHENTICATION_AUDIENCE=api://servicecontrol

rem ServicePulse client configuration (Primary instance only)
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_CLIENTID={servicepulse-client-id}
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_AUTHORITY=https://login.microsoftonline.com/{tenant-id}/v2.0
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_APISCOPES=["api://servicecontrol/access_as_user"]

rem Forwarded headers - trust only your reverse proxy
set SERVICECONTROL_FORWARDEDHEADERS_ENABLED=true
set SERVICECONTROL_FORWARDEDHEADERS_TRUSTALLPROXIES=false
set SERVICECONTROL_FORWARDEDHEADERS_KNOWNPROXIES=10.0.0.5
rem Or use CIDR notation:
rem set SERVICECONTROL_FORWARDEDHEADERS_KNOWNNETWORKS=10.0.0.0/24

rem HTTP to HTTPS redirect (optional)
set SERVICECONTROL_HTTPS_REDIRECTHTTPTOHTTPS=true
set SERVICECONTROL_HTTPS_PORT=443

rem Restrict CORS
set SERVICECONTROL_CORS_ALLOWEDORIGINS=https://servicepulse
```

#### ServiceControl.Audit Configuration

**App.config:**

```xml
<appSettings>
  <!-- Host and Port -->
  <add key="ServiceControl.Audit/Hostname" value="localhost" />
  <add key="ServiceControl.Audit/Port" value="44444" />

  <!-- Authentication (same Authority and Audience as Primary) -->
  <add key="ServiceControl.Audit/Authentication.Enabled" value="true" />
  <add key="ServiceControl.Audit/Authentication.Authority" value="https://login.microsoftonline.com/{tenant-id}/v2.0" />
  <add key="ServiceControl.Audit/Authentication.Audience" value="api://servicecontrol" />

  <!-- Forwarded headers -->
  <add key="ServiceControl.Audit/ForwardedHeaders.Enabled" value="true" />
  <add key="ServiceControl.Audit/ForwardedHeaders.TrustAllProxies" value="false" />
  <add key="ServiceControl.Audit/ForwardedHeaders.KnownProxies" value="10.0.0.5" />

  <!-- Restrict CORS -->
  <add key="ServiceControl.Audit/Cors.AllowedOrigins" value="https://servicepulse" />
</appSettings>
```

**Environment variables:**

```cmd
rem Host and Port
set SERVICECONTROL_AUDIT_HOSTNAME=localhost
set SERVICECONTROL_AUDIT_PORT=44444

rem Authentication (same Authority and Audience as Primary)
set SERVICECONTROL_AUDIT_AUTHENTICATION_ENABLED=true
set SERVICECONTROL_AUDIT_AUTHENTICATION_AUTHORITY=https://login.microsoftonline.com/{tenant-id}/v2.0
set SERVICECONTROL_AUDIT_AUTHENTICATION_AUDIENCE=api://servicecontrol

rem Forwarded headers
set SERVICECONTROL_AUDIT_FORWARDEDHEADERS_ENABLED=true
set SERVICECONTROL_AUDIT_FORWARDEDHEADERS_TRUSTALLPROXIES=false
set SERVICECONTROL_AUDIT_FORWARDEDHEADERS_KNOWNPROXIES=10.0.0.5

rem Restrict CORS
set SERVICECONTROL_AUDIT_CORS_ALLOWEDORIGINS=https://servicepulse
```

#### ServiceControl.Monitoring Configuration

**App.config:**

```xml
<appSettings>
  <!-- Host and Port -->
  <add key="Monitoring/Hostname" value="localhost" />
  <add key="Monitoring/Port" value="33633" />

  <!-- Authentication (same Authority and Audience as Primary) -->
  <add key="Monitoring/Authentication.Enabled" value="true" />
  <add key="Monitoring/Authentication.Authority" value="https://login.microsoftonline.com/{tenant-id}/v2.0" />
  <add key="Monitoring/Authentication.Audience" value="api://servicecontrol" />

  <!-- Forwarded headers -->
  <add key="Monitoring/ForwardedHeaders.Enabled" value="true" />
  <add key="Monitoring/ForwardedHeaders.TrustAllProxies" value="false" />
  <add key="Monitoring/ForwardedHeaders.KnownProxies" value="10.0.0.5" />

  <!-- Restrict CORS -->
  <add key="Monitoring/Cors.AllowedOrigins" value="https://servicepulse" />
</appSettings>
```

**Environment variables:**

```cmd
rem Host and Port
set MONITORING_HOSTNAME=localhost
set MONITORING_PORT=33633

rem Authentication (same Authority and Audience as Primary)
set MONITORING_AUTHENTICATION_ENABLED=true
set MONITORING_AUTHENTICATION_AUTHORITY=https://login.microsoftonline.com/{tenant-id}/v2.0
set MONITORING_AUTHENTICATION_AUDIENCE=api://servicecontrol

rem Forwarded headers
set MONITORING_FORWARDEDHEADERS_ENABLED=true
set MONITORING_FORWARDEDHEADERS_TRUSTALLPROXIES=false
set MONITORING_FORWARDEDHEADERS_KNOWNPROXIES=10.0.0.5

rem Restrict CORS
set MONITORING_CORS_ALLOWEDORIGINS=https://servicepulse
```

---

### Scenario 2: Direct HTTPS with Authentication

Kestrel handles TLS directly without a reverse proxy. Suitable for simpler deployments or when a reverse proxy is not available.

**Architecture:**

```text
Client → HTTPS → ServiceControl (Kestrel)
                 (TLS + JWT validation)
```

**Security Features:**

| Feature                 | Status                |
|-------------------------|-----------------------|
| JWT Authentication      | ✅ Enabled             |
| Kestrel HTTPS           | ✅ Enabled             |
| HSTS                    | ✅ Enabled             |
| Restricted CORS Origins | ✅ Enabled             |
| Forwarded Headers       | ❌ Disabled (no proxy) |
| Restricted Proxy Trust  | N/A                   |

> **Note:** HTTPS redirection is not configured in this scenario because clients connect directly over HTTPS. There is no HTTP endpoint exposed that would need to redirect. HTTPS redirection is only useful when a reverse proxy handles SSL termination and ServiceControl needs to redirect HTTP requests to the proxy's HTTPS endpoint.

#### Primary Instance Configuration (Direct HTTPS)

**App.config:**

```xml
<appSettings>
  <!-- Host and Port -->
  <add key="ServiceControl/Hostname" value="servicecontrol" />
  <add key="ServiceControl/Port" value="33333" />

  <!-- Remote Audit Instance(s) -->
  <add key="ServiceControl/RemoteInstances" value="[{&quot;api_uri&quot;:&quot;https://servicecontrol-audit:44444/api&quot;}]" />

  <!-- Kestrel HTTPS -->
  <add key="ServiceControl/Https.Enabled" value="true" />
  <add key="ServiceControl/Https.CertificatePath" value="C:\certs\servicecontrol.pfx" />
  <add key="ServiceControl/Https.CertificatePassword" value="your-certificate-password" />
  <add key="ServiceControl/Https.EnableHsts" value="true" />
  <add key="ServiceControl/Https.HstsMaxAgeSeconds" value="31536000" />

  <!-- Authentication -->
  <add key="ServiceControl/Authentication.Enabled" value="true" />
  <add key="ServiceControl/Authentication.Authority" value="https://login.microsoftonline.com/{tenant-id}/v2.0" />
  <add key="ServiceControl/Authentication.Audience" value="api://servicecontrol" />

  <!-- ServicePulse client configuration -->
  <add key="ServiceControl/Authentication.ServicePulse.ClientId" value="{servicepulse-client-id}" />
  <add key="ServiceControl/Authentication.ServicePulse.Authority" value="https://login.microsoftonline.com/{tenant-id}/v2.0" />
  <add key="ServiceControl/Authentication.ServicePulse.ApiScopes" value="[&quot;api://servicecontrol/access_as_user&quot;]" />

  <!-- Restrict CORS -->
  <add key="ServiceControl/Cors.AllowedOrigins" value="https://servicepulse" />

  <!-- No forwarded headers (no proxy) -->
  <add key="ServiceControl/ForwardedHeaders.Enabled" value="false" />
</appSettings>
```

**Environment variables:**

```cmd
rem Host and Port
set SERVICECONTROL_HOSTNAME=servicecontrol
set SERVICECONTROL_PORT=33333

rem Remote Audit Instance(s)
set SERVICECONTROL_REMOTEINSTANCES=[{"api_uri":"https://servicecontrol-audit:44444/api"}]

rem Kestrel HTTPS
set SERVICECONTROL_HTTPS_ENABLED=true
set SERVICECONTROL_HTTPS_CERTIFICATEPATH=C:\certs\servicecontrol.pfx
set SERVICECONTROL_HTTPS_CERTIFICATEPASSWORD=your-certificate-password
set SERVICECONTROL_HTTPS_ENABLEHSTS=true
set SERVICECONTROL_HTTPS_HSTSMAXAGESECONDS=31536000

rem Authentication
set SERVICECONTROL_AUTHENTICATION_ENABLED=true
set SERVICECONTROL_AUTHENTICATION_AUTHORITY=https://login.microsoftonline.com/{tenant-id}/v2.0
set SERVICECONTROL_AUTHENTICATION_AUDIENCE=api://servicecontrol

rem ServicePulse client configuration
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_CLIENTID={servicepulse-client-id}
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_AUTHORITY=https://login.microsoftonline.com/{tenant-id}/v2.0
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_APISCOPES=["api://servicecontrol/access_as_user"]

rem Restrict CORS
set SERVICECONTROL_CORS_ALLOWEDORIGINS=https://servicepulse

rem No forwarded headers (no proxy)
set SERVICECONTROL_FORWARDEDHEADERS_ENABLED=false
```

#### Audit Instance Configuration (Direct HTTPS)

**App.config:**

```xml
<appSettings>
  <!-- Host and Port -->
  <add key="ServiceControl.Audit/Hostname" value="servicecontrol-audit" />
  <add key="ServiceControl.Audit/Port" value="44444" />

  <!-- Kestrel HTTPS -->
  <add key="ServiceControl.Audit/Https.Enabled" value="true" />
  <add key="ServiceControl.Audit/Https.CertificatePath" value="C:\certs\servicecontrol-audit.pfx" />
  <add key="ServiceControl.Audit/Https.CertificatePassword" value="your-certificate-password" />
  <add key="ServiceControl.Audit/Https.EnableHsts" value="true" />

  <!-- Authentication -->
  <add key="ServiceControl.Audit/Authentication.Enabled" value="true" />
  <add key="ServiceControl.Audit/Authentication.Authority" value="https://login.microsoftonline.com/{tenant-id}/v2.0" />
  <add key="ServiceControl.Audit/Authentication.Audience" value="api://servicecontrol" />

  <!-- Restrict CORS -->
  <add key="ServiceControl.Audit/Cors.AllowedOrigins" value="https://servicepulse" />

  <!-- No forwarded headers -->
  <add key="ServiceControl.Audit/ForwardedHeaders.Enabled" value="false" />
</appSettings>
```

**Environment variables:**

```cmd
rem Host and Port
set SERVICECONTROL_AUDIT_HOSTNAME=servicecontrol-audit
set SERVICECONTROL_AUDIT_PORT=44444

rem Kestrel HTTPS
set SERVICECONTROL_AUDIT_HTTPS_ENABLED=true
set SERVICECONTROL_AUDIT_HTTPS_CERTIFICATEPATH=C:\certs\servicecontrol-audit.pfx
set SERVICECONTROL_AUDIT_HTTPS_CERTIFICATEPASSWORD=your-certificate-password
set SERVICECONTROL_AUDIT_HTTPS_ENABLEHSTS=true

rem Authentication
set SERVICECONTROL_AUDIT_AUTHENTICATION_ENABLED=true
set SERVICECONTROL_AUDIT_AUTHENTICATION_AUTHORITY=https://login.microsoftonline.com/{tenant-id}/v2.0
set SERVICECONTROL_AUDIT_AUTHENTICATION_AUDIENCE=api://servicecontrol

rem Restrict CORS
set SERVICECONTROL_AUDIT_CORS_ALLOWEDORIGINS=https://servicepulse

rem No forwarded headers
set SERVICECONTROL_AUDIT_FORWARDEDHEADERS_ENABLED=false
```

#### Monitoring Instance Configuration (Direct HTTPS)

**App.config:**

```xml
<appSettings>
  <!-- Host and Port -->
  <add key="Monitoring/Hostname" value="servicecontrol-monitoring" />
  <add key="Monitoring/Port" value="33633" />

  <!-- Kestrel HTTPS -->
  <add key="Monitoring/Https.Enabled" value="true" />
  <add key="Monitoring/Https.CertificatePath" value="C:\certs\servicecontrol-monitoring.pfx" />
  <add key="Monitoring/Https.CertificatePassword" value="your-certificate-password" />
  <add key="Monitoring/Https.EnableHsts" value="true" />

  <!-- Authentication -->
  <add key="Monitoring/Authentication.Enabled" value="true" />
  <add key="Monitoring/Authentication.Authority" value="https://login.microsoftonline.com/{tenant-id}/v2.0" />
  <add key="Monitoring/Authentication.Audience" value="api://servicecontrol" />

  <!-- Restrict CORS -->
  <add key="Monitoring/Cors.AllowedOrigins" value="https://servicepulse" />

  <!-- No forwarded headers -->
  <add key="Monitoring/ForwardedHeaders.Enabled" value="false" />
</appSettings>
```

**Environment variables:**

```cmd
rem Host and Port
set MONITORING_HOSTNAME=servicecontrol-monitoring
set MONITORING_PORT=33633

rem Kestrel HTTPS
set MONITORING_HTTPS_ENABLED=true
set MONITORING_HTTPS_CERTIFICATEPATH=C:\certs\servicecontrol-monitoring.pfx
set MONITORING_HTTPS_CERTIFICATEPASSWORD=your-certificate-password
set MONITORING_HTTPS_ENABLEHSTS=true

rem Authentication
set MONITORING_AUTHENTICATION_ENABLED=true
set MONITORING_AUTHENTICATION_AUTHORITY=https://login.microsoftonline.com/{tenant-id}/v2.0
set MONITORING_AUTHENTICATION_AUDIENCE=api://servicecontrol

rem Restrict CORS
set MONITORING_CORS_ALLOWEDORIGINS=https://servicepulse

rem No forwarded headers
set MONITORING_FORWARDEDHEADERS_ENABLED=false
```

---

### Scenario 3: End-to-End Encryption with Reverse Proxy

For environments requiring encryption of internal traffic. The reverse proxy terminates external TLS and re-encrypts traffic to ServiceControl over HTTPS.

**Architecture:**

```text
Client → HTTPS → Reverse Proxy → HTTPS → ServiceControl (Kestrel)
                 (TLS termination)       (TLS + JWT validation)
```

**Security Features:**

| Feature                    | Status                   |
|----------------------------|--------------------------|
| JWT Authentication         | ✅ Enabled                |
| Kestrel HTTPS              | ✅ Enabled                |
| HTTPS Redirection          | N/A (no HTTP endpoint)   |
| HSTS                       | N/A (configure at proxy) |
| Restricted CORS Origins    | ✅ Enabled                |
| Forwarded Headers          | ✅ Enabled                |
| Restricted Proxy Trust     | ✅ Enabled                |
| Internal Traffic Encrypted | ✅ Yes                    |

> **Note:** HTTPS redirection and HSTS are not applicable in this scenario because ServiceControl only exposes an HTTPS endpoint (Kestrel HTTPS is enabled). There is no HTTP endpoint to redirect from. The reverse proxy is responsible for redirecting external HTTP requests to HTTPS and sending HSTS headers to browsers. Compare this to Scenario 1, where Kestrel HTTPS is disabled and ServiceControl exposes an HTTP endpoint - in that case, HTTPS redirection can optionally be enabled as defense-in-depth.

#### Primary Instance Configuration (End-to-End Encryption)

**App.config:**

```xml
<appSettings>
  <!-- Host and Port -->
  <add key="ServiceControl/Hostname" value="localhost" />
  <add key="ServiceControl/Port" value="33333" />

  <!-- Remote Audit Instance(s) -->
  <add key="ServiceControl/RemoteInstances" value="[{&quot;api_uri&quot;:&quot;https://servicecontrol-audit/api&quot;}]" />

  <!-- Kestrel HTTPS for internal encryption -->
  <add key="ServiceControl/Https.Enabled" value="true" />
  <add key="ServiceControl/Https.CertificatePath" value="C:\certs\servicecontrol-internal.pfx" />
  <add key="ServiceControl/Https.CertificatePassword" value="your-certificate-password" />

  <!-- Authentication -->
  <add key="ServiceControl/Authentication.Enabled" value="true" />
  <add key="ServiceControl/Authentication.Authority" value="https://login.microsoftonline.com/{tenant-id}/v2.0" />
  <add key="ServiceControl/Authentication.Audience" value="api://servicecontrol" />

  <!-- ServicePulse client configuration -->
  <add key="ServiceControl/Authentication.ServicePulse.ClientId" value="{servicepulse-client-id}" />
  <add key="ServiceControl/Authentication.ServicePulse.Authority" value="https://login.microsoftonline.com/{tenant-id}/v2.0" />
  <add key="ServiceControl/Authentication.ServicePulse.ApiScopes" value="[&quot;api://servicecontrol/access_as_user&quot;]" />

  <!-- Forwarded headers - trust only your reverse proxy -->
  <add key="ServiceControl/ForwardedHeaders.Enabled" value="true" />
  <add key="ServiceControl/ForwardedHeaders.TrustAllProxies" value="false" />
  <add key="ServiceControl/ForwardedHeaders.KnownProxies" value="10.0.0.5" />

  <!-- Restrict CORS -->
  <add key="ServiceControl/Cors.AllowedOrigins" value="https://servicepulse" />
</appSettings>
```

**Environment variables:**

```cmd
rem Host and Port
set SERVICECONTROL_HOSTNAME=localhost
set SERVICECONTROL_PORT=33333

rem Remote Audit Instance(s)
set SERVICECONTROL_REMOTEINSTANCES=[{"api_uri":"https://servicecontrol-audit/api"}]

rem Kestrel HTTPS for internal encryption
set SERVICECONTROL_HTTPS_ENABLED=true
set SERVICECONTROL_HTTPS_CERTIFICATEPATH=C:\certs\servicecontrol-internal.pfx
set SERVICECONTROL_HTTPS_CERTIFICATEPASSWORD=your-certificate-password

rem Authentication
set SERVICECONTROL_AUTHENTICATION_ENABLED=true
set SERVICECONTROL_AUTHENTICATION_AUTHORITY=https://login.microsoftonline.com/{tenant-id}/v2.0
set SERVICECONTROL_AUTHENTICATION_AUDIENCE=api://servicecontrol

rem ServicePulse client configuration
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_CLIENTID={servicepulse-client-id}
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_AUTHORITY=https://login.microsoftonline.com/{tenant-id}/v2.0
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_APISCOPES=["api://servicecontrol/access_as_user"]

rem Forwarded headers - trust only your reverse proxy
set SERVICECONTROL_FORWARDEDHEADERS_ENABLED=true
set SERVICECONTROL_FORWARDEDHEADERS_TRUSTALLPROXIES=false
set SERVICECONTROL_FORWARDEDHEADERS_KNOWNPROXIES=10.0.0.5

rem Restrict CORS
set SERVICECONTROL_CORS_ALLOWEDORIGINS=https://servicepulse
```

> **Note:** Audit and Monitoring instances follow the same pattern. See Scenario 1 for the authentication and forwarded headers configuration, and add the HTTPS settings as shown above.

---

## Certificate Management

### Certificate Requirements

- Use certificates from a trusted Certificate Authority (CA) for production
- For internal deployments, an internal/enterprise CA is acceptable
- Certificates must include the hostname in the Subject Alternative Name (SAN)
- Minimum key size: 2048-bit RSA or 256-bit ECC

### Certificate Formats

ServiceControl supports PFX (PKCS#12) certificate files:

```xml
<add key="ServiceControl/Https.CertificatePath" value="C:\certs\servicecontrol.pfx" />
<add key="ServiceControl/Https.CertificatePassword" value="your-certificate-password" />
```

### Certificate Storage Best Practices

1. **File permissions**: Restrict access to certificate files to the service account running ServiceControl
2. **Password protection**: Use strong passwords for PFX files
3. **Secure storage**: Store certificates in a secure location with appropriate access controls
4. **Avoid source control**: Never commit certificates or passwords to source control

### Certificate Renewal

1. Obtain a new certificate before the current one expires
2. Replace the certificate file at the configured path
3. Restart the ServiceControl service to load the new certificate
4. Verify HTTPS connectivity after restart

---

## Configuration Reference

### Authentication Settings

| Setting                                   | Type   | Default | Description                                                 |
|-------------------------------------------|--------|---------|-------------------------------------------------------------|
| `Authentication.Enabled`                  | bool   | `false` | Enable JWT Bearer authentication                            |
| `Authentication.Authority`                | string | -       | OpenID Connect authority URL (required when enabled)        |
| `Authentication.Audience`                 | string | -       | Expected audience for tokens (required when enabled)        |
| `Authentication.ValidateIssuer`           | bool   | `true`  | Validate token issuer                                       |
| `Authentication.ValidateAudience`         | bool   | `true`  | Validate token audience                                     |
| `Authentication.ValidateLifetime`         | bool   | `true`  | Validate token expiration                                   |
| `Authentication.ValidateIssuerSigningKey` | bool   | `true`  | Validate token signing key                                  |
| `Authentication.RequireHttpsMetadata`     | bool   | `true`  | Require HTTPS for metadata endpoint                         |
| `Authentication.ServicePulse.ClientId`    | string | -       | OAuth client ID for ServicePulse (Primary only)             |
| `Authentication.ServicePulse.Authority`   | string | -       | Authority URL for ServicePulse (defaults to main Authority) |
| `Authentication.ServicePulse.ApiScopes`   | string | -       | API scopes for ServicePulse to request                      |

### HTTPS Settings

| Setting                       | Type   | Default    | Description                                            |
|-------------------------------|--------|------------|--------------------------------------------------------|
| `Https.Enabled`               | bool   | `false`    | Enable Kestrel HTTPS with certificate                  |
| `Https.CertificatePath`       | string | -          | Path to PFX certificate file                           |
| `Https.CertificatePassword`   | string | -          | Certificate password                                   |
| `Https.RedirectHttpToHttps`   | bool   | `false`    | Redirect HTTP requests to HTTPS                        |
| `Https.Port`                  | int    | -          | HTTPS port for redirects (required with reverse proxy) |
| `Https.EnableHsts`            | bool   | `false`    | Enable HTTP Strict Transport Security                  |
| `Https.HstsMaxAgeSeconds`     | int    | `31536000` | HSTS max-age in seconds (1 year)                       |
| `Https.HstsIncludeSubDomains` | bool   | `false`    | Include subdomains in HSTS                             |

### Forwarded Headers Settings

> **⚠️ Security Warning:** Never set `TrustAllProxies` to `true` in production when ServiceControl is accessible from untrusted networks. This can allow attackers to spoof client IP addresses and bypass security controls.

| Setting                            | Type   | Default | Description                                   |
|------------------------------------|--------|---------|-----------------------------------------------|
| `ForwardedHeaders.Enabled`         | bool   | `true`  | Enable forwarded headers processing           |
| `ForwardedHeaders.TrustAllProxies` | bool   | `true`  | Trust X-Forwarded-* from any source           |
| `ForwardedHeaders.KnownProxies`    | string | -       | Comma-separated list of trusted proxy IPs     |
| `ForwardedHeaders.KnownNetworks`   | string | -       | Comma-separated list of trusted CIDR networks |

> **Note:** If `KnownProxies` or `KnownNetworks` are configured, `TrustAllProxies` is automatically set to `false`.

### CORS Settings

| Setting               | Type   | Default | Description                             |
|-----------------------|--------|---------|-----------------------------------------|
| `Cors.AllowAnyOrigin` | bool   | `true`  | Allow requests from any origin          |
| `Cors.AllowedOrigins` | string | -       | Comma-separated list of allowed origins |

> **Note:** If `AllowedOrigins` is configured, `AllowAnyOrigin` is automatically set to `false`.

### Host Settings

| Setting    | Type   | Default     | Description           |
|------------|--------|-------------|-----------------------|
| `Hostname` | string | `localhost` | Hostname to listen on |
| `Port`     | int    | varies      | Port to listen on     |

### Remote Instance Settings (Primary Only)

| Setting           | Type   | Default | Description                              |
|-------------------|--------|---------|------------------------------------------|
| `RemoteInstances` | string | -       | JSON array of remote Audit instance URIs |

---

## Scenario Comparison Matrix

| Feature                        | Reverse Proxy + Auth | Direct HTTPS + Auth | End-to-End Encryption |
|--------------------------------|:--------------------:|:-------------------:|:---------------------:|
| **JWT Authentication**         |          ✅           |          ✅          |           ✅           |
| **Kestrel HTTPS**              |          ❌           |          ✅          |           ✅           |
| **HTTPS Redirection**          |     ✅ (optional)     |          ✅          |     ❌ (at proxy)      |
| **HSTS**                       |     ❌ (at proxy)     |          ✅          |     ❌ (at proxy)      |
| **Restricted CORS**            |          ✅           |          ✅          |           ✅           |
| **Forwarded Headers**          |          ✅           |          ❌          |           ✅           |
| **Restricted Proxy Trust**     |          ✅           |         N/A         |           ✅           |
| **Internal Traffic Encrypted** |          ❌           |          ✅          |           ✅           |
|                                |                      |                     |                       |
| **Requires Reverse Proxy**     |         Yes          |         No          |          Yes          |
| **Certificate Management**     |    At proxy only     |  At ServiceControl  |         Both          |

---

## See Also

- [Authentication Configuration](authentication.md) - Detailed authentication setup guide
- [HTTPS Configuration](https-configuration.md) - Detailed HTTPS setup guide
- [Forwarded Headers Configuration](forwarded-headers.md) - Forwarded headers reference
- [Reverse Proxy Testing](reverseproxy-testing.md) - Local testing with NGINX
- [Authentication Testing](authentication-testing.md) - Testing authentication scenarios
