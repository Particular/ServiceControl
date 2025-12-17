# Local Testing with NGINX Reverse Proxy

This guide provides scenario-based tests for ServiceControl instances behind an NGINX reverse proxy. Use this to verify:

- SSL/TLS termination at the reverse proxy
- Forwarded headers handling (`X-Forwarded-For`, `X-Forwarded-Proto`, `X-Forwarded-Host`)
- HTTP to HTTPS redirection
- HSTS (HTTP Strict Transport Security)
- WebSocket support (SignalR)

## Instance Reference

| Instance                  | Project Directory               | Default Port | Hostname                           | Environment Variable Prefix |
|---------------------------|---------------------------------|--------------|------------------------------------|-----------------------------|
| ServiceControl (Primary)  | `src\ServiceControl`            | 33333        | `servicecontrol.localhost`         | `SERVICECONTROL_`           |
| ServiceControl.Audit      | `src\ServiceControl.Audit`      | 44444        | `servicecontrol-audit.localhost`   | `SERVICECONTROL_AUDIT_`     |
| ServiceControl.Monitoring | `src\ServiceControl.Monitoring` | 33633        | `servicecontrol-monitor.localhost` | `MONITORING_`               |

## Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) installed and running
- [mkcert](https://github.com/FiloSottile/mkcert) for generating local development certificates
- ServiceControl built locally (see main README for build instructions)
- curl (included with Windows 10/11, Git Bash, or WSL)

### Installing mkcert

**Windows (using Chocolatey):**

```cmd
choco install mkcert
```

**Windows (using Scoop):**

```cmd
scoop install mkcert
```

After installing, run `mkcert -install` to install the local CA in your system trust store.

## Setup

### Step 1: Create the Local Development Folder

Create a `.local` folder in the repository root (this folder is gitignored):

```cmd
mkdir .local
mkdir .local\certs
```

### Step 2: Generate SSL Certificates

Use mkcert to generate trusted local development certificates:

```cmd
mkcert -install
cd .local\certs
mkcert -cert-file local-platform.pem -key-file local-platform-key.pem servicecontrol.localhost servicecontrol-audit.localhost servicecontrol-monitor.localhost localhost
```

### Step 3: Create Docker Compose Configuration

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

### Step 4: Create NGINX Configuration

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

    # ServiceControl (Primary) - HTTPS
    server {
        listen 443 ssl;
        server_name servicecontrol.localhost;

        location / {
            proxy_pass http://host.docker.internal:33333;

            # WebSocket Support
            proxy_http_version 1.1;
            proxy_set_header Upgrade $http_upgrade;
            proxy_set_header Connection $connection_upgrade;

            # Forwarded Headers
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
            proxy_set_header X-Forwarded-Host $host;
        }
    }

    # ServiceControl (Primary) - HTTP (for testing HTTP-to-HTTPS redirect)
    server {
        listen 80;
        server_name servicecontrol.localhost;

        location / {
            proxy_pass http://host.docker.internal:33333;

            # WebSocket Support
            proxy_http_version 1.1;
            proxy_set_header Upgrade $http_upgrade;
            proxy_set_header Connection $connection_upgrade;

            # Forwarded Headers
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
            proxy_set_header X-Forwarded-Host $host;
        }
    }

    # ServiceControl.Audit - HTTPS
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
            proxy_set_header X-Forwarded-Host $host;
        }
    }

    # ServiceControl.Audit - HTTP (for testing HTTP-to-HTTPS redirect)
    server {
        listen 80;
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
            proxy_set_header X-Forwarded-Host $host;
        }
    }

    # ServiceControl.Monitoring - HTTPS
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
            proxy_set_header X-Forwarded-Host $host;
        }
    }

    # ServiceControl.Monitoring - HTTP (for testing HTTP-to-HTTPS redirect)
    server {
        listen 80;
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
            proxy_set_header X-Forwarded-Host $host;
        }
    }
}
```

### Step 5: Configure Hosts File

Add the following entries to your hosts file (`C:\Windows\System32\drivers\etc\hosts`):

```text
127.0.0.1 servicecontrol.localhost
127.0.0.1 servicecontrol-audit.localhost
127.0.0.1 servicecontrol-monitor.localhost
```

### Step 6: Start the NGINX Reverse Proxy

From the repository root:

```cmd
docker compose -f .local/compose.yml up -d
```

### Step 7: Final Directory Structure

After completing the setup, your `.local` folder should look like:

```text
.local/
├── compose.yml
├── nginx.conf
└── certs/
    ├── local-platform.pem
    └── local-platform-key.pem
```

## Test Scenarios

> **Important:** ServiceControl must be running before testing. A 502 Bad Gateway error means NGINX cannot reach ServiceControl.
> **Note:** Use `TRUSTALLPROXIES=true` for local Docker testing. The NGINX container's IP address varies based on Docker's network configuration (e.g., `172.x.x.x`), making it impractical to specify a fixed `KNOWNPROXIES` value.

### Scenario 1: HTTPS Access

Verify that HTTPS is working through the reverse proxy.

**Clear environment variables and start ServiceControl:**

```cmd
set SERVICECONTROL_FORWARDEDHEADERS_ENABLED=
set SERVICECONTROL_FORWARDEDHEADERS_TRUSTALLPROXIES=
set SERVICECONTROL_HTTPS_REDIRECTHTTPTOHTTPS=
set SERVICECONTROL_HTTPS_PORT=
set SERVICECONTROL_HTTPS_ENABLEHSTS=

cd src\ServiceControl
dotnet run --no-launch-profile
```

**Test with curl:**

```cmd
curl -k -v https://servicecontrol.localhost/api 2>&1 | findstr /C:"HTTP/"
```

**Expected output:**

```text
< HTTP/1.1 200 OK
```

The request succeeds over HTTPS through the NGINX reverse proxy.

### Scenario 2: Forwarded Headers Processing

Verify that forwarded headers are being processed correctly.

**Clear environment variables and start ServiceControl:**

```cmd
set SERVICECONTROL_FORWARDEDHEADERS_ENABLED=true
set SERVICECONTROL_FORWARDEDHEADERS_TRUSTALLPROXIES=true
set SERVICECONTROL_HTTPS_REDIRECTHTTPTOHTTPS=
set SERVICECONTROL_HTTPS_PORT=
set SERVICECONTROL_HTTPS_ENABLEHSTS=

cd src\ServiceControl
dotnet run --no-launch-profile
```

**Test with curl:**

```cmd
curl -k https://servicecontrol.localhost/debug/request-info | json
```

**Expected output:**

```json
{
  "processed": {
    "scheme": "https",
    "host": "servicecontrol.localhost",
    "remoteIpAddress": "172.x.x.x"
  },
  "rawHeaders": {
    "xForwardedFor": "",
    "xForwardedProto": "",
    "xForwardedHost": ""
  },
  "configuration": {
    "enabled": true,
    "trustAllProxies": true,
    "knownProxies": [],
    "knownNetworks": []
  }
}
```

The key indicators that forwarded headers are working:

- `processed.scheme` is `https` (from `X-Forwarded-Proto`)
- `processed.host` is `servicecontrol.localhost` (from `X-Forwarded-Host`)
- `rawHeaders` are empty because the middleware consumed them (trusted proxy)

### Scenario 3: HTTP to HTTPS Redirect

Verify that HTTP requests are redirected to HTTPS.

**Clear environment variables and start ServiceControl:**

```cmd
set SERVICECONTROL_FORWARDEDHEADERS_ENABLED=true
set SERVICECONTROL_FORWARDEDHEADERS_TRUSTALLPROXIES=true
set SERVICECONTROL_HTTPS_REDIRECTHTTPTOHTTPS=true
set SERVICECONTROL_HTTPS_PORT=443
set SERVICECONTROL_HTTPS_ENABLEHSTS=

cd src\ServiceControl
dotnet run --no-launch-profile
```

**Test with curl:**

```cmd
curl -v http://servicecontrol.localhost/api 2>&1 | findstr /i location
```

**Expected output:**

```text
< Location: https://servicecontrol.localhost/api
```

HTTP requests are redirected to HTTPS with a 307 (Temporary Redirect) status.

### Scenario 4: HSTS

Verify that the HSTS header is included in HTTPS responses.

> **Note:** HSTS is disabled in Development environment. You must use `--no-launch-profile` to prevent launchSettings.json from overriding it.

**Clear environment variables and start ServiceControl:**

```cmd
set SERVICECONTROL_FORWARDEDHEADERS_ENABLED=true
set SERVICECONTROL_FORWARDEDHEADERS_TRUSTALLPROXIES=true
set SERVICECONTROL_HTTPS_REDIRECTHTTPTOHTTPS=
set SERVICECONTROL_HTTPS_PORT=
set SERVICECONTROL_HTTPS_ENABLEHSTS=true

cd src\ServiceControl
dotnet run --environment Production --no-launch-profile
```

**Test with curl:**

```cmd
curl -k -v https://servicecontrol.localhost/api 2>&1 | findstr /i strict-transport-security
```

**Expected output:**

```text
< Strict-Transport-Security: max-age=31536000
```

The HSTS header is present with the default max-age of 1 year.

## Testing Other Instances

The scenarios above use ServiceControl (Primary). To test ServiceControl.Audit or ServiceControl.Monitoring:

1. Use the appropriate environment variable prefix (see Configuration Reference below)
2. Use the corresponding project directory and hostname

| Instance                  | Project Directory               | Hostname                           | Env Var Prefix          |
|---------------------------|---------------------------------|------------------------------------|-------------------------|
| ServiceControl (Primary)  | `src\ServiceControl`            | `servicecontrol.localhost`         | `SERVICECONTROL_`       |
| ServiceControl.Audit      | `src\ServiceControl.Audit`      | `servicecontrol-audit.localhost`   | `SERVICECONTROL_AUDIT_` |
| ServiceControl.Monitoring | `src\ServiceControl.Monitoring` | `servicecontrol-monitor.localhost` | `MONITORING_`           |

## Configuration Reference

| Environment Variable                        | Default    | Description                                 |
|---------------------------------------------|------------|---------------------------------------------|
| `{PREFIX}_FORWARDEDHEADERS_ENABLED`         | `true`     | Enable forwarded headers processing         |
| `{PREFIX}_FORWARDEDHEADERS_TRUSTALLPROXIES` | `true`     | Trust all proxies                           |
| `{PREFIX}_FORWARDEDHEADERS_KNOWNPROXIES`    | -          | Comma-separated list of trusted proxy IPs   |
| `{PREFIX}_FORWARDEDHEADERS_KNOWNNETWORKS`   | -          | Comma-separated list of trusted CIDR ranges |
| `{PREFIX}_HTTPS_REDIRECTHTTPTOHTTPS`        | `false`    | Redirect HTTP to HTTPS                      |
| `{PREFIX}_HTTPS_PORT`                       | -          | HTTPS port for redirect                     |
| `{PREFIX}_HTTPS_ENABLEHSTS`                 | `false`    | Enable HSTS                                 |
| `{PREFIX}_HTTPS_HSTSMAXAGESECONDS`          | `31536000` | HSTS max-age (1 year)                       |
| `{PREFIX}_HTTPS_HSTSINCLUDESUBDOMAINS`      | `false`    | Include subdomains in HSTS                  |

Where `{PREFIX}` is:

- `SERVICECONTROL` for ServiceControl (Primary)
- `SERVICECONTROL_AUDIT` for ServiceControl.Audit
- `MONITORING` for ServiceControl.Monitoring

## Cleanup

### Stop NGINX

```cmd
docker compose -f .local/compose.yml down
```

### Clear Environment Variables

After testing, clear the environment variables:

**Command Prompt (cmd):**

```cmd
set SERVICECONTROL_FORWARDEDHEADERS_ENABLED=
set SERVICECONTROL_FORWARDEDHEADERS_TRUSTALLPROXIES=
set SERVICECONTROL_HTTPS_REDIRECTHTTPTOHTTPS=
set SERVICECONTROL_HTTPS_PORT=
set SERVICECONTROL_HTTPS_ENABLEHSTS=
```

**PowerShell:**

```powershell
$env:SERVICECONTROL_FORWARDEDHEADERS_ENABLED = $null
$env:SERVICECONTROL_FORWARDEDHEADERS_TRUSTALLPROXIES = $null
$env:SERVICECONTROL_HTTPS_REDIRECTHTTPTOHTTPS = $null
$env:SERVICECONTROL_HTTPS_PORT = $null
$env:SERVICECONTROL_HTTPS_ENABLEHSTS = $null
```

### Remove Hosts Entries (Optional)

If you no longer need the hostnames, remove these entries from your hosts file (`C:\Windows\System32\drivers\etc\hosts`):

```text
127.0.0.1 servicecontrol.localhost
127.0.0.1 servicecontrol-audit.localhost
127.0.0.1 servicecontrol-monitor.localhost
```

## Troubleshooting

### 502 Bad Gateway

This error means NGINX cannot reach ServiceControl. Check:

1. ServiceControl is running (`dotnet run` in the appropriate project directory)
2. ServiceControl is accessible directly: `curl http://localhost:33333/api`
3. Docker Desktop is running and `host.docker.internal` resolves correctly

### "Connection refused" errors

Ensure ServiceControl instances are running and listening on the expected ports:

- ServiceControl (Primary): 33333
- ServiceControl.Audit: 44444
- ServiceControl.Monitoring: 33633

### Headers not being applied

1. Verify `FORWARDEDHEADERS_ENABLED` is `true`
2. Verify `FORWARDEDHEADERS_TRUSTALLPROXIES` is `true` (for local Docker testing)
3. Use the `/debug/request-info` endpoint to check current settings

### Certificate errors in browser

1. Ensure mkcert's root CA is installed: `mkcert -install`
2. Restart your browser after installing the root CA

### Docker networking issues

If using Docker Desktop on Windows with WSL2:

- Ensure `host.docker.internal` resolves correctly
- Check that the ServiceControl ports are not blocked by Windows Firewall

### Debug endpoint not available

The `/debug/request-info` endpoint is only available when running in Development environment (the default when using `dotnet run`).

## See Also

- [Hosting Guide](hosting-guide.md) - Configuration reference for all deployment scenarios
- [Forwarded Headers Testing](forward-headers-testing.md) - Testing forwarded headers without a reverse proxy
