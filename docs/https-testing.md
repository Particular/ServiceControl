# Local Testing with Direct HTTPS

This guide provides scenario-based tests for ServiceControl's direct HTTPS features. Use this to verify Kestrel HTTPS behavior without a reverse proxy.

> [!NOTE]
> HTTP to HTTPS redirection (`RedirectHttpToHttps`) is designed for reverse proxy scenarios where the proxy forwards HTTP requests to ServiceControl. When running with direct HTTPS, ServiceControl only binds to a single port (HTTPS). To test HTTP to HTTPS redirection, see [Reverse Proxy Testing](reverseproxy-testing.md).

## Instance Reference

| Instance                  | Project Directory               | Default Port | Environment Variable Prefix | App.config Key Prefix   |
|---------------------------|---------------------------------|--------------|-----------------------------|-------------------------|
| ServiceControl (Primary)  | `src\ServiceControl`            | 33333        | `SERVICECONTROL_`           | `ServiceControl/`       |
| ServiceControl.Audit      | `src\ServiceControl.Audit`      | 44444        | `SERVICECONTROL_AUDIT_`     | `ServiceControl.Audit/` |
| ServiceControl.Monitoring | `src\ServiceControl.Monitoring` | 33633        | `MONITORING_`               | `Monitoring/`           |

> [!NOTE]
> Environment variables must include the instance prefix (e.g., `SERVICECONTROL_HTTPS_ENABLED` for the primary instance).

## Prerequisites

- [mkcert](https://github.com/FiloSottile/mkcert) for generating local development certificates
- ServiceControl built locally (see [main README for instructions](../README.md#how-to-rundebug-locally))
- curl (included with Windows 10/11, Git Bash, or WSL)
- (Optional) For formatted JSON output: `npm install -g json` then pipe curl output through `| json`

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

All scenarios use environment variables for configuration.

> [!NOTE]
> The `RemoteInstances` setting on the primary ServiceControl instance needs the correct schema. e.g.; `https://localhost:44444/api/`

### Test Grouping by Configuration

Both scenarios use the same HTTPS configuration, so you only need to start the service once to run all tests.

## HTTPS Enabled Configuration

**Start the instance once, then run all tests (Scenarios 1, 2).**

```cmd
rem ServiceControl (Primary)
set SERVICECONTROL_HTTPS_ENABLED=true
set SERVICECONTROL_HTTPS_CERTIFICATEPATH=C:\path\to\ServiceControl\.local\certs\localhost.pfx
set SERVICECONTROL_HTTPS_CERTIFICATEPASSWORD=changeit
set SERVICECONTROL_HTTPS_REDIRECTHTTPTOHTTPS=
set SERVICECONTROL_HTTPS_PORT=
set SERVICECONTROL_HTTPS_ENABLEHSTS=
set SERVICECONTROL_FORWARDEDHEADERS_ENABLED=false
set SERVICECONTROL_REMOTEINSTANCES=[{"api_uri":"https://localhost:44444"}]

rem ServiceControl.Audit
set SERVICECONTROL_AUDIT_HTTPS_ENABLED=true
set SERVICECONTROL_AUDIT_HTTPS_CERTIFICATEPATH=C:\path\to\ServiceControl\.local\certs\localhost.pfx
set SERVICECONTROL_AUDIT_HTTPS_CERTIFICATEPASSWORD=changeit
set SERVICECONTROL_AUDIT_HTTPS_REDIRECTHTTPTOHTTPS=
set SERVICECONTROL_AUDIT_HTTPS_PORT=
set SERVICECONTROL_AUDIT_HTTPS_ENABLEHSTS=
set SERVICECONTROL_AUDIT_FORWARDEDHEADERS_ENABLED=false

rem ServiceControl.Monitoring
set MONITORING_HTTPS_ENABLED=true
set MONITORING_HTTPS_CERTIFICATEPATH=C:\path\to\ServiceControl\.local\certs\localhost.pfx
set MONITORING_HTTPS_CERTIFICATEPASSWORD=changeit
set MONITORING_HTTPS_REDIRECTHTTPTOHTTPS=
set MONITORING_HTTPS_PORT=
set MONITORING_HTTPS_ENABLEHSTS=
set MONITORING_FORWARDEDHEADERS_ENABLED=false

dotnet run
```

### Scenario 1: Basic HTTPS Connectivity

Verify that HTTPS is working with a valid certificate.

**Test with curl:**

```cmd
rem ServiceControl (Primary)
curl --ssl-no-revoke -v https://localhost:33333/api 2>&1 | findstr /C:"HTTP/" /C:"SSL"

rem ServiceControl.Audit
curl --ssl-no-revoke -v https://localhost:44444/api 2>&1 | findstr /C:"HTTP/" /C:"SSL"

rem ServiceControl.Monitoring
curl --ssl-no-revoke -v https://localhost:33633/ 2>&1 | findstr /C:"HTTP/" /C:"SSL"
```

> [!NOTE]
> The `--ssl-no-revoke` flag is required on Windows because mkcert certificates don't have CRL distribution points, causing `CRYPT_E_NO_REVOCATION_CHECK` errors.

**Expected output:**

```text
* schannel: renegotiating SSL/TLS connection
* schannel: SSL/TLS connection renegotiated
< HTTP/1.1 200 OK
```

The request succeeds over HTTPS. The exact SSL output varies by curl version and platform, but you should see `HTTP/1.1 200 OK` confirming success.

### Scenario 2: HTTP Disabled (HTTPS Only)

Verify that HTTP requests fail when only HTTPS is enabled.

**Test with curl (using configuration above, attempting HTTP):**

```cmd
rem ServiceControl (Primary)
curl http://localhost:33333/api

rem ServiceControl.Audit
curl http://localhost:44444/api

rem ServiceControl.Monitoring
curl http://localhost:33633/
```

**Expected output:**

```text
curl: (52) Empty reply from server
```

HTTP requests fail because Kestrel is listening for HTTPS but receives plaintext HTTP, which it cannot process. The server closes the connection without responding.

> [!NOTE]
> HSTS is not tested locally because ASP.NET Core excludes localhost from HSTS by default (to prevent accidentally caching HSTS during development). HSTS will work correctly in production with non-localhost hostnames.

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
rem ServiceControl (Primary)
set SERVICECONTROL_HTTPS_ENABLED=
set SERVICECONTROL_HTTPS_CERTIFICATEPATH=
set SERVICECONTROL_HTTPS_CERTIFICATEPASSWORD=
set SERVICECONTROL_HTTPS_ENABLEHSTS=
set SERVICECONTROL_HTTPS_HSTSMAXAGESECONDS=
set SERVICECONTROL_HTTPS_HSTSINCLUDESUBDOMAINS=
set SERVICECONTROL_FORWARDEDHEADERS_ENABLED=
set SERVICECONTROL_REMOTEINSTANCES=[{"api_uri":"http://localhost:44444"}]

rem ServiceControl.Audit
set SERVICECONTROL_AUDIT_HTTPS_ENABLED=
set SERVICECONTROL_AUDIT_HTTPS_CERTIFICATEPATH=
set SERVICECONTROL_AUDIT_HTTPS_CERTIFICATEPASSWORD=
set SERVICECONTROL_AUDIT_HTTPS_ENABLEHSTS=
set SERVICECONTROL_AUDIT_HTTPS_HSTSMAXAGESECONDS=
set SERVICECONTROL_AUDIT_HTTPS_HSTSINCLUDESUBDOMAINS=
set SERVICECONTROL_AUDIT_FORWARDEDHEADERS_ENABLED=

rem ServiceControl.Monitoring
set MONITORING_HTTPS_ENABLED=
set MONITORING_HTTPS_CERTIFICATEPATH=
set MONITORING_HTTPS_CERTIFICATEPASSWORD=
set MONITORING_HTTPS_ENABLEHSTS=
set MONITORING_HTTPS_HSTSMAXAGESECONDS=
set MONITORING_HTTPS_HSTSINCLUDESUBDOMAINS=
set MONITORING_FORWARDEDHEADERS_ENABLED=
```

## See Also

- [Hosting Guide](https://docs.particular.net/servicecontrol/security/hosting-guide) - Detailed configuration reference for all deployment scenarios
- [Reverse Proxy Testing](reverseproxy-testing.md) - Testing with a reverse proxy (NGINX)
- [Forwarded Headers Testing](forward-headers-testing.md) - Testing forwarded headers without a reverse proxy
