# Local Testing with Direct HTTPS

This guide provides scenario-based tests for ServiceControl's direct HTTPS features. Use this to verify Kestrel HTTPS behavior without a reverse proxy.

> **Note:** HTTP to HTTPS redirection (`RedirectHttpToHttps`) is designed for reverse proxy scenarios where the proxy forwards HTTP requests to ServiceControl. When running with direct HTTPS, ServiceControl only binds to a single port (HTTPS). To test HTTP to HTTPS redirection, see [Reverse Proxy Testing](reverseproxy-testing.md).

## Instance Reference

| Instance                  | Project Directory               | Default Port | Environment Variable Prefix | App.config Key Prefix   |
|---------------------------|---------------------------------|--------------|-----------------------------|-------------------------|
| ServiceControl (Primary)  | `src\ServiceControl`            | 33333        | `SERVICECONTROL_`           | `ServiceControl/`       |
| ServiceControl.Audit      | `src\ServiceControl.Audit`      | 44444        | `SERVICECONTROL_AUDIT_`     | `ServiceControl.Audit/` |
| ServiceControl.Monitoring | `src\ServiceControl.Monitoring` | 33633        | `MONITORING_`               | `Monitoring/`           |

> **Note:** Environment variables must include the instance prefix (e.g., `SERVICECONTROL_HTTPS_ENABLED` for the primary instance).

## Prerequisites

- [mkcert](https://github.com/FiloSottile/mkcert) for generating local development certificates
- ServiceControl built locally (see main README for build instructions)
- curl (included with Windows 10/11, Git Bash, or WSL)

## Enabling Debug Logs

To enable detailed logging for troubleshooting, set the `LogLevel` environment variable before starting each instance:

```cmd
rem ServiceControl Primary
set SERVICECONTROL_LOGLEVEL=Debug

rem ServiceControl.Audit
set SERVICECONTROL_AUDIT_LOGLEVEL=Debug

rem ServiceControl.Monitoring
set MONITORING_LOGLEVEL=Debug
```

**Valid log levels:** `Trace`, `Debug`, `Information` (or `Info`), `Warning` (or `Warn`), `Error`, `Critical` (or `Fatal`), `None` (or `Off`)

Debug logs will show detailed HTTPS configuration and certificate loading information.

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

## Setup

### Step 1: Create the Local Development Folder

Create a `.local` folder in the repository root (this folder is gitignored):

```bash
mkdir .local
mkdir .local/certs
```

### Step 2: Generate PFX Certificates

Kestrel requires certificates in PFX format. Use mkcert to generate them:

```bash
# Install mkcert's root CA (one-time setup)
mkcert -install

# Navigate to the certs folder
cd .local/certs

# Generate PFX certificate for localhost
mkcert -p12-file localhost.pfx -pkcs12 localhost 127.0.0.1 ::1 servicecontrol servicecontrol-audit servicecontrol-monitor
```

When prompted for a password, you can use an empty password by pressing Enter, or set a password (e.g., `changeit`) and note it for the configuration step.

## Test Scenarios

All scenarios use environment variables for configuration. Run each scenario from the `src/ServiceControl` directory.

### Scenario 1: Basic HTTPS Connectivity

Verify that HTTPS is working with a valid certificate.

**Cleanup and start ServiceControl:**

```cmd
set SERVICECONTROL_HTTPS_ENABLED=true
set SERVICECONTROL_HTTPS_CERTIFICATEPATH=C:\path\to\ServiceControl\.local\certs\localhost.pfx
set SERVICECONTROL_HTTPS_CERTIFICATEPASSWORD=changeit
set SERVICECONTROL_HTTPS_REDIRECTHTTPTOHTTPS=
set SERVICECONTROL_HTTPS_PORT=
set SERVICECONTROL_HTTPS_ENABLEHSTS=
set SERVICECONTROL_FORWARDEDHEADERS_ENABLED=false

dotnet run
```

**Test with curl:**

```cmd
curl --ssl-no-revoke -v https://localhost:33333/api 2>&1 | findstr /C:"HTTP/" /C:"SSL"
```

> **Note:** The `--ssl-no-revoke` flag is required on Windows because mkcert certificates don't have CRL distribution points, causing `CRYPT_E_NO_REVOCATION_CHECK` errors.

**Expected output:**

```text
* schannel: SSL/TLS connection renegotiated
< HTTP/1.1 200 OK
```

The request succeeds over HTTPS. The exact SSL output varies by curl version and platform, but you should see `HTTP/1.1 200 OK` confirming success.

### Scenario 2: HTTP Disabled (HTTPS Only)

Verify that HTTP requests fail when only HTTPS is enabled.

**Cleanup and start ServiceControl:**

```cmd
set SERVICECONTROL_HTTPS_ENABLED=true
set SERVICECONTROL_HTTPS_CERTIFICATEPATH=C:\path\to\ServiceControl\.local\certs\localhost.pfx
set SERVICECONTROL_HTTPS_CERTIFICATEPASSWORD=changeit
set SERVICECONTROL_HTTPS_REDIRECTHTTPTOHTTPS=
set SERVICECONTROL_HTTPS_PORT=
set SERVICECONTROL_HTTPS_ENABLEHSTS=
set SERVICECONTROL_FORWARDEDHEADERS_ENABLED=false

dotnet run
```

**Test with curl (HTTP):**

```cmd
curl http://localhost:33333/api
```

**Expected output:**

```text
curl: (52) Empty reply from server
```

HTTP requests fail because Kestrel is listening for HTTPS but receives plaintext HTTP, which it cannot process. The server closes the connection without responding.

## HTTPS Configuration Reference

| App.config Key                | Environment Variable (Primary)               | Default    | Description                                          |
|-------------------------------|----------------------------------------------|------------|------------------------------------------------------|
| `Https.Enabled`               | `SERVICECONTROL_HTTPS_ENABLED`               | `false`    | Enable Kestrel HTTPS                                 |
| `Https.CertificatePath`       | `SERVICECONTROL_HTTPS_CERTIFICATEPATH`       | -          | Path to PFX certificate file                         |
| `Https.CertificatePassword`   | `SERVICECONTROL_HTTPS_CERTIFICATEPASSWORD`   | -          | Certificate password (empty string for no password)  |
| `Https.RedirectHttpToHttps`   | `SERVICECONTROL_HTTPS_REDIRECTHTTPTOHTTPS`   | `false`    | Redirect HTTP requests to HTTPS (reverse proxy only) |
| `Https.EnableHsts`            | `SERVICECONTROL_HTTPS_ENABLEHSTS`            | `false`    | Enable HTTP Strict Transport Security                |
| `Https.HstsMaxAgeSeconds`     | `SERVICECONTROL_HTTPS_HSTSMAXAGESECONDS`     | `31536000` | HSTS max-age (1 year)                                |
| `Https.HstsIncludeSubDomains` | `SERVICECONTROL_HTTPS_HSTSINCLUDESUBDOMAINS` | `false`    | Include subdomains in HSTS                           |

> **Note:** For other instances, replace the `SERVICECONTROL_` prefix with the appropriate instance prefix (see Instance Reference table).
>
> **Note:** HSTS is not tested locally because ASP.NET Core excludes localhost from HSTS by default (to prevent accidentally caching HSTS during development). HSTS will work correctly in production with non-localhost hostnames.

## Testing Other Instances

The scenarios above use ServiceControl (Primary). To test ServiceControl.Audit or ServiceControl.Monitoring:

1. Use the appropriate environment variable prefix (see Instance Reference above)
2. Use the corresponding project directory and port

| Instance                  | Project Directory               | Port  | Env Var Prefix          |
|---------------------------|---------------------------------|-------|-------------------------|
| ServiceControl (Primary)  | `src\ServiceControl`            | 33333 | `SERVICECONTROL_`       |
| ServiceControl.Audit      | `src\ServiceControl.Audit`      | 44444 | `SERVICECONTROL_AUDIT_` |
| ServiceControl.Monitoring | `src\ServiceControl.Monitoring` | 33633 | `MONITORING_`           |

## Troubleshooting

### Certificate not found

Ensure the `CertificatePath` is an absolute path and the file exists.

### Certificate password incorrect

If you set a password when generating the PFX, ensure it matches `CertificatePassword` in the config.

### Certificate errors in browser/curl

1. Ensure mkcert's root CA is installed: `mkcert -install`
2. Restart your browser after installing the root CA

### CRYPT_E_NO_REVOCATION_CHECK error in curl

Windows curl fails to check certificate revocation for mkcert certificates because they don't have CRL distribution points. Use the `--ssl-no-revoke` flag:

```cmd
curl --ssl-no-revoke https://localhost:33333/api
```

### Port already in use

Ensure no other process is using the ServiceControl ports (33333, 44444, 33633).

## Cleanup

After testing, clear the environment variables:

**Command Prompt (cmd):**

```cmd
set SERVICECONTROL_HTTPS_ENABLED=
set SERVICECONTROL_HTTPS_CERTIFICATEPATH=
set SERVICECONTROL_HTTPS_CERTIFICATEPASSWORD=
set SERVICECONTROL_HTTPS_ENABLEHSTS=
set SERVICECONTROL_HTTPS_HSTSMAXAGESECONDS=
set SERVICECONTROL_HTTPS_HSTSINCLUDESUBDOMAINS=
set SERVICECONTROL_FORWARDEDHEADERS_ENABLED=
```

**PowerShell:**

```powershell
$env:SERVICECONTROL_HTTPS_ENABLED = $null
$env:SERVICECONTROL_HTTPS_CERTIFICATEPATH = $null
$env:SERVICECONTROL_HTTPS_CERTIFICATEPASSWORD = $null
$env:SERVICECONTROL_HTTPS_ENABLEHSTS = $null
$env:SERVICECONTROL_HTTPS_HSTSMAXAGESECONDS = $null
$env:SERVICECONTROL_HTTPS_HSTSINCLUDESUBDOMAINS = $null
$env:SERVICECONTROL_FORWARDEDHEADERS_ENABLED = $null
```

## See Also

- [Hosting Guide](hosting-guide.md) - Detailed configuration reference for all deployment scenarios
- [Reverse Proxy Testing](reverseproxy-testing.md) - Testing with a reverse proxy (NGINX)
- [Forwarded Headers Testing](forward-headers-testing.md) - Testing forwarded headers without a reverse proxy
