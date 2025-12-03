# Local Testing with Direct HTTPS

This guide explains how to test ServiceControl with direct HTTPS enabled on Kestrel, without using a reverse proxy. This is useful for testing scenarios like:

- Direct TLS termination at ServiceControl
- HTTPS redirection
- HSTS (HTTP Strict Transport Security)
- End-to-end encryption testing

## Prerequisites

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

## Step 2: Generate PFX Certificates

Kestrel requires certificates in PFX format. Use mkcert to generate them:

```bash
# Install mkcert's root CA (one-time setup)
mkcert -install

# Navigate to the certs folder
cd .local/certs

# Generate PFX certificate for localhost
mkcert -p12-file localhost.pfx -pkcs12 localhost 127.0.0.1 ::1
```

When prompted for a password, you can use an empty password by pressing Enter, or set a password and note it for the configuration step.

## Step 3: Configure ServiceControl Instances

Configure HTTPS in the `App.config` file for each ServiceControl instance. See [HTTPS Settings](hosting-guide.md#https-settings) in the Hosting Guide for all available options.

| Instance | Config Key Prefix | App.config Location |
|----------|-------------------|---------------------|
| ServiceControl (Primary) | `ServiceControl/` | `src/ServiceControl/App.config` |
| ServiceControl.Audit | `ServiceControl.Audit/` | `src/ServiceControl.Audit/App.config` |
| ServiceControl.Monitoring | `Monitoring/` | `src/ServiceControl.Monitoring/App.config` |

Example for ServiceControl (Primary):

```xml
<appSettings>
  <!-- Enable Kestrel HTTPS -->
  <add key="ServiceControl/Https.Enabled" value="true" />
  <add key="ServiceControl/Https.CertificatePath" value="C:\path\to\repo\.local\certs\localhost.pfx" />
  <add key="ServiceControl/Https.CertificatePassword" value="" />

  <!-- Optional: Enable HSTS -->
  <add key="ServiceControl/Https.EnableHsts" value="true" />

  <!-- Optional: Redirect HTTP to HTTPS -->
  <add key="ServiceControl/Https.RedirectHttpToHttps" value="true" />

  <!-- Disable forwarded headers (no reverse proxy) -->
  <add key="ServiceControl/ForwardedHeaders.Enabled" value="false" />
</appSettings>
```

> **Note:** Replace `C:\path\to\repo` with the actual path to your ServiceControl repository. Use the full absolute path to the PFX file.

## Step 4: Start ServiceControl Instances

Start the ServiceControl instances locally using your preferred method:

### **Option A: Visual Studio**

1. Open `src/ServiceControl.sln`
2. Run the desired project(s) with the appropriate launch profile

### **Option B: Command Line**

```bash
# Run ServiceControl (Primary)
dotnet run --project src/ServiceControl/ServiceControl.csproj

# Run ServiceControl.Audit
dotnet run --project src/ServiceControl.Audit/ServiceControl.Audit.csproj

# Run ServiceControl.Monitoring
dotnet run --project src/ServiceControl.Monitoring/ServiceControl.Monitoring.csproj
```

## Step 5: Verify the Setup

Test that HTTPS is working correctly:

```bash
# Test ServiceControl (Primary)
curl https://localhost:33333/api

# Test ServiceControl.Audit
curl https://localhost:44444/api

# Test ServiceControl.Monitoring
curl https://localhost:33633/api
```

If you've installed mkcert's root CA, the requests should succeed without certificate warnings.

### Testing HTTPS Redirection

If `RedirectHttpToHttps` is enabled, HTTP requests should redirect to HTTPS:

```bash
# This should redirect to https://localhost:33333/api
curl -v http://localhost:33333/api
```

### Testing HSTS

If `EnableHsts` is enabled, the response should include the `Strict-Transport-Security` header:

```bash
curl -v https://localhost:33333/api 2>&1 | grep -i strict-transport-security
```

## HTTPS Configuration Reference

| Setting | Default | Description |
|---------|---------|-------------|
| `Https.Enabled` | `false` | Enable Kestrel HTTPS |
| `Https.CertificatePath` | - | Path to PFX certificate file |
| `Https.CertificatePassword` | - | Certificate password (empty string for no password) |
| `Https.RedirectHttpToHttps` | `false` | Redirect HTTP requests to HTTPS |
| `Https.EnableHsts` | `false` | Enable HTTP Strict Transport Security |
| `Https.HstsMaxAgeSeconds` | `31536000` | HSTS max-age (1 year) |
| `Https.HstsIncludeSubDomains` | `false` | Include subdomains in HSTS |

## Troubleshooting

### Certificate not found

Ensure the `CertificatePath` is an absolute path and the file exists.

### Certificate password incorrect

If you set a password when generating the PFX, ensure it matches `CertificatePassword` in the config.

### Certificate errors in browser

1. Ensure mkcert's root CA is installed: `mkcert -install`
2. Restart your browser after installing the root CA

### Port already in use

Ensure no other process is using the ServiceControl ports (33333, 44444, 33633).

## See Also

- [Hosting Guide](hosting-guide.md) - Detailed configuration reference for all deployment scenarios
- [Local NGINX Testing](local-nginx-testing.md) - Testing with a reverse proxy
