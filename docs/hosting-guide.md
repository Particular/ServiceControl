# ServiceControl Production Hosting Guide

See [ServiceControl Hosting Guide](https://docs.particular.net/servicecontrol/security/hosting-guide) on the public docs site.

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

### Certificate Renewal

1. Obtain a new certificate before the current one expires
2. Replace the certificate file at the configured path
3. Restart the ServiceControl service to load the new certificate
4. Verify HTTPS connectivity after restart

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

## See Also

- [Authentication Configuration](authentication.md) - Detailed authentication setup guide
- [HTTPS Configuration](https-configuration.md) - Detailed HTTPS setup guide
- [Forwarded Headers Configuration](forwarded-headers.md) - Forwarded headers reference
- [Reverse Proxy Testing](reverseproxy-testing.md) - Local testing with NGINX
- [Authentication Testing](authentication-testing.md) - Testing authentication scenarios
