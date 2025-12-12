# Local Testing with NGINX Reverse Proxy

This guide explains how to set up a local development environment with NGINX as a reverse proxy in front of ServiceControl instances. This is useful for testing scenarios like:

- SSL/TLS termination at the reverse proxy
- Forwarded headers handling (`X-Forwarded-For`, `X-Forwarded-Proto`, `X-Forwarded-Host`)
- Testing CORS configuration
- Simulating production deployment topology

## Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) installed and running
- [mkcert](https://github.com/FiloSottile/mkcert) for generating local development certificates
- ServiceControl built locally (see main README for build instructions)

### Installing mkcert

**Windows (using Chocolatey):**

```powershell
choco install mkcert
```

**Windows (using Scoop):**

```powershell
scoop install mkcert
```

**macOS (using Homebrew):**

```bash
brew install mkcert
```

**Linux:**

```bash
# Debian/Ubuntu
sudo apt install libnss3-tools
# Then download from https://github.com/FiloSottile/mkcert/releases

# Arch Linux
sudo pacman -S mkcert
```

After installing, run `mkcert -install` to install the local CA in your system trust store.

## Step 1: Create the Local Development Folder

Create a `.local` folder in the repository root (this folder is gitignored):

```bash
mkdir .local
mkdir .local/certs
```

## Step 2: Generate SSL Certificates

Use mkcert to generate trusted local development certificates:

```bash
# Install mkcert's root CA (one-time setup)
mkcert -install

# Navigate to the certs folder
cd .local/certs

# Generate certificates for all ServiceControl hostnames
mkcert -cert-file local-platform.pem -key-file local-platform-key.pem \
  servicecontrol.localhost \
  servicecontrol-audit.localhost \
  servicecontrol-monitor.localhost \
  localhost
```

## Step 3: Create Docker Compose Configuration

Create `.local/compose.yml`:

```yaml
services:
  reverse-proxy-servicecontrol:
    image: nginx:alpine
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./nginx.conf:/etc/nginx/nginx.conf:ro
      - ./certs/local-platform.pem:/etc/nginx/certs/local.pem:ro
      - ./certs/local-platform-key.pem:/etc/nginx/certs/local-key.pem:ro
```

Ensure no other NGINX containers are running.

## Step 4: Create NGINX Configuration

Create `.local/nginx.conf`:

```nginx
events { worker_connections 1024; }

http {
    # WebSocket support: set connection to 'upgrade' if Upgrade header present
    map $http_upgrade $connection_upgrade {
        default upgrade;
        ''      close;
    }

    # Shared SSL Settings
    ssl_certificate     /etc/nginx/certs/local.pem;
    ssl_certificate_key /etc/nginx/certs/local-key.pem;
    ssl_protocols       TLSv1.2 TLSv1.3;
    ssl_ciphers         HIGH:!aNULL:!MD5;

    # ServiceControl (Primary) - 443
    server {
        listen 443 ssl;
        server_name servicecontrol.localhost;

        location / {
            proxy_pass http://host.docker.internal:44444;

            # WebSocket Support
            proxy_http_version 1.1;
            proxy_set_header Upgrade $http_upgrade;
            proxy_set_header Connection $connection_upgrade;

            # Forwarded Headers
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
        }
    }

    # ServiceControl (Primary) - 80 - Used to test HTTP-HTTPS redirection
    server {
        listen 80;
        server_name servicecontrol.localhost;

        location / {
            proxy_pass http://host.docker.internal:44444;

            # WebSocket Support
            proxy_http_version 1.1;
            proxy_set_header Upgrade $http_upgrade;
            proxy_set_header Connection $connection_upgrade;

            # Forwarded Headers
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
        }
    }

    # ServiceControl.Audit
    server {
        listen 443 ssl;
        server_name servicecontrol-audit.localhost;

        location / {
            proxy_pass http://host.docker.internal:44444;

            # WebSocket Support
            proxy_http_version 1.1;
            proxy_set_header Upgrade $http_upgrade;
            proxy_set_header Connection $connection_upgrade;

            # Forwarded Headers
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
        }
    }

    # ServiceControl.Monitoring
    server {
        listen 443 ssl;
        server_name servicecontrol-monitor.localhost;

        location / {
            proxy_pass http://host.docker.internal:33633;

            # WebSocket Support
            proxy_http_version 1.1;
            proxy_set_header Upgrade $http_upgrade;
            proxy_set_header Connection $connection_upgrade;

            # Forwarded Headers
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
        }
    }
}
```

## Step 5: Configure Hosts File

Add the following entries to your hosts file:

**Windows:** `C:\Windows\System32\drivers\etc\hosts`
**Linux/macOS:** `/etc/hosts`

```text
127.0.0.1 servicecontrol.localhost
127.0.0.1 servicecontrol-audit.localhost
127.0.0.1 servicecontrol-monitor.localhost
```

## Step 6: Configure ServiceControl Instances

Configure forwarded headers in the `App.config` file for each ServiceControl instance. See [Forwarded Headers Settings](hosting-guide.md#forwarded-headers-settings) in the Hosting Guide for all available options.

For local testing with this NGINX setup, set `KnownProxies` to `127.0.0.1`:

| Instance | Config Key Prefix | App.config Location |
|----------|-------------------|---------------------|
| ServiceControl (Primary) | `ServiceControl/` | `src/ServiceControl/App.config` |
| ServiceControl.Audit | `ServiceControl.Audit/` | `src/ServiceControl.Audit/App.config` |
| ServiceControl.Monitoring | `Monitoring/` | `src/ServiceControl.Monitoring/App.config` |

Example for ServiceControl (Primary):

```xml
<appSettings>
  <add key="ServiceControl/ForwardedHeaders.Enabled" value="true" />
  <add key="ServiceControl/ForwardedHeaders.KnownProxies" value="127.0.0.1" />
</appSettings>
```

> **Note:** The `KnownProxies` value is `127.0.0.1` because NGINX running in Docker connects to the host via `host.docker.internal`, which resolves to `127.0.0.1` on the host machine.

## Step 7: Start the NGINX Reverse Proxy

From the repository root:

```bash
docker compose -f .local/compose.yml up -d
```

This starts an NGINX container that:

- Listens on ports 80 (HTTP) and 443 (HTTPS)
- Terminates SSL/TLS using the mkcert certificates
- Proxies requests to ServiceControl instances running on the host

## Step 8: Start ServiceControl Instances

Start the ServiceControl instances locally using your preferred method:

### **Option A: Visual Studio**

1. Open `src/ServiceControl.sln`
2. Run the desired project(s) with the appropriate launch profile

### **Option B: Command Line**

Navigate to the project folder.

```bash
# Run ServiceControl (Primary)
dotnet build
dotnet run

# Run ServiceControl.Audit
dotnet run --project src/ServiceControl.Audit/ServiceControl.Audit.csproj

# Run ServiceControl.Monitoring
dotnet run --project src/ServiceControl.Monitoring/ServiceControl.Monitoring.csproj
```

## Step 9: Verify the Setup

Test that the reverse proxy is working correctly:

When running in the Development environment, a `/debug/request-info` endpoint is available to diagnose forwarded headers configuration:

```powershell
# Direct to ServiceControl (bypassing proxy)
Invoke-RestMethod http://localhost:33333/debug/request-info | ConvertTo-Json -Depth 5

# Through the reverse proxy (skip certificate check for self-signed certs)
Invoke-RestMethod https://servicecontrol.localhost/debug/request-info -SkipCertificateCheck | ConvertTo-Json -Depth 5
```

This endpoint returns detailed information including:

- **processed**: Request values after forwarded headers processing
- **rawHeaders**: Raw `X-Forwarded-*` header values (empty if consumed by middleware)
- **configuration**: Current forwarded headers configuration

Example response:

```json
{
  "processed": {
    "scheme": "https",
    "host": "servicecontrol.localhost",
    "remoteIpAddress": "172.17.0.1"
  },
  "rawHeaders": {
    "xForwardedFor": "",
    "xForwardedProto": "",
    "xForwardedHost": ""
  },
  "configuration": {
    "enabled": true,
    "trustAllProxies": false,
    "knownProxies": ["127.0.0.1"],
    "knownNetworks": []
  }
}
```

### Key Diagnostic Questions

1. **Were headers applied?** - If `rawHeaders` are empty but `processed` values changed, the middleware consumed and applied them
2. **Why weren't headers applied?** - If `rawHeaders` still contain values, the middleware didn't trust the caller. Check `knownProxies` and `knownNetworks` in `configuration`
3. **Is forwarded headers enabled?** - Check `configuration.enabled`

> **Note:** This endpoint is only available when `ASPNETCORE_ENVIRONMENT` is set to `Development`.

## Final Directory Structure

After completing the setup, your `.local` folder should look like:

```text
.local/
├── compose.yml
├── nginx.conf
└── certs/
    ├── local-platform.pem
    └── local-platform-key.pem
```

## NGINX Configuration Reference

| Server Name | HTTPS Port | Backend Port | Instance |
|------------|------------|--------------|----------|
| `servicecontrol.localhost` | 443 | 33333 | ServiceControl (Primary) |
| `servicecontrol-audit.localhost` | 443 | 44444 | ServiceControl.Audit |
| `servicecontrol-monitor.localhost` | 443 | 33633 | ServiceControl.Monitoring |

Each server block:

- Terminates SSL/TLS
- Sets forwarded headers (`X-Forwarded-For`, `X-Forwarded-Proto`, `X-Forwarded-Host`)
- Supports WebSocket connections (for SignalR)
- Proxies to `host.docker.internal` to reach the host machine

## Forwarded Headers Behavior

When `ForwardedHeaders.KnownProxies` is configured correctly:

- `Request.Scheme` will be `https` (from `X-Forwarded-Proto`)
- `Request.Host` will be the external hostname (from `X-Forwarded-Host`)
- Client IP will be available from `X-Forwarded-For`

When the proxy is **not** trusted (incorrect `KnownProxies`):

- `X-Forwarded-*` headers are **ignored** (not applied to the request)
- `Request.Scheme` remains `http`
- `Request.Host` remains the internal hostname
- The request is still processed (not blocked)

## Testing HTTP to HTTPS Redirection

The `RedirectHttpToHttps` setting enables ASP.NET Core's HTTPS redirection middleware. This is designed for reverse proxy scenarios where:

1. The proxy forwards HTTP requests to ServiceControl
2. The proxy sends `X-Forwarded-Proto: http` to indicate the original protocol
3. ServiceControl responds with a 307 redirect to the HTTPS URL

### Configure ServiceControl for Redirection

Add the following to your `App.config`:

```xml
<appSettings>
  <add key="ServiceControl/ForwardedHeaders.Enabled" value="true" />
  <add key="ServiceControl/ForwardedHeaders.KnownProxies" value="127.0.0.1" />
  <add key="ServiceControl/Https.RedirectHttpToHttps" value="true" />
</appSettings>
```

Or use environment variables:

```cmd
set SERVICECONTROL_FORWARDEDHEADERS_ENABLED=true
set SERVICECONTROL_FORWARDEDHEADERS_KNOWNPROXIES=127.0.0.1
set SERVICECONTROL_HTTPS_REDIRECTHTTPTOHTTPS=true

dotnet run
```

### Test the Redirection

The NGINX configuration includes an HTTP server on port 80 that forwards `X-Forwarded-Proto: http`. Test with curl:

```bash
# Request via HTTP - should receive a 307 redirect to HTTPS
curl -v http://servicecontrol.localhost/api 2>&1 | grep -E "< HTTP|< Location"
```

**Expected output:**

```text
< HTTP/1.1 307 Temporary Redirect
< Location: https://servicecontrol.localhost/api
```

The middleware detects `X-Forwarded-Proto: http` and redirects the client to the HTTPS URL.

### Verify Without Redirection

With `RedirectHttpToHttps` disabled (or not set), HTTP requests are processed normally:

```bash
# Request via HTTP - should receive 200 OK (no redirect)
curl -v http://servicecontrol.localhost/api 2>&1 | grep "< HTTP"
```

**Expected output:**

```text
< HTTP/1.1 200 OK
```

### How It Works

1. Client sends HTTP request to `http://servicecontrol.localhost/api`
2. NGINX receives on port 80 and forwards to ServiceControl with `X-Forwarded-Proto: http`
3. ServiceControl's forwarded headers middleware processes the header (from trusted proxy)
4. `Request.Scheme` is set to `http` based on `X-Forwarded-Proto`
5. HTTPS redirection middleware sees `Request.Scheme == "http"` and issues a 307 redirect
6. Client follows redirect to `https://servicecontrol.localhost/api`

> **Note:** This redirection only works with a reverse proxy because ServiceControl needs to receive the `X-Forwarded-Proto` header to know the original protocol. Without a proxy, ServiceControl only binds to a single port and cannot perform HTTP to HTTPS redirection. See [Local HTTPS Testing](local-https-testing.md) for direct HTTPS scenarios.

## Troubleshooting

### "Connection refused" errors

Ensure the ServiceControl instances are running and listening on the expected ports.

### Headers not being applied

1. Verify `ForwardedHeaders.Enabled` is `true`
2. Check that `KnownProxies` includes `127.0.0.1`
3. Review the ServiceControl logs for forwarded headers configuration messages

### Certificate errors in browser

1. Ensure mkcert's root CA is installed: `mkcert -install`
2. Restart your browser after installing the root CA

### Docker networking issues

If using Docker Desktop on Windows with WSL2:

- Ensure `host.docker.internal` resolves correctly
- Check that the ServiceControl ports are not blocked by Windows Firewall

## Stopping the Environment

```bash
docker compose -f .local/compose.yml down
```

## See Also

- [Hosting Guide](hosting-guide.md) - Detailed configuration reference for all deployment scenarios
