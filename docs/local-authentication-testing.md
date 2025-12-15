# Local Testing Authentication

This guide explains how to test authentication configuration for ServiceControl instances. This approach uses curl to test authentication enforcement and configuration endpoints.

## Prerequisites

- ServiceControl built locally (see main README for build instructions)
- curl (included with Windows 10/11, Git Bash, or WSL)
- (Optional) For formatted JSON output: `npm install -g json` then pipe curl output through `| json`
- (Optional) An OIDC provider for full end-to-end testing (e.g., Microsoft Entra ID, Auth0, Okta)

## Instance Reference

| Instance | Project Directory | Default Port | Environment Variable Prefix |
|----------|-------------------|--------------|----------------------------|
| ServiceControl (Primary) | `src\ServiceControl` | 33333 | `SERVICECONTROL_` |
| ServiceControl.Audit | `src\ServiceControl.Audit` | 44444 | `SERVICECONTROL_AUDIT_` |
| ServiceControl.Monitoring | `src\ServiceControl.Monitoring` | 33633 | `MONITORING_` |

## How Authentication Works

When authentication is enabled:

1. All API requests must include a valid JWT bearer token in the `Authorization` header
2. ServiceControl validates the token against the configured OIDC authority
3. Requests without a valid token receive a `401 Unauthorized` response
4. The `/api/authentication/configuration` endpoint returns authentication configuration for clients (like ServicePulse)

## Configuration Methods

Settings can be configured via:

1. **Environment variables** (recommended for testing) - Easy to change between scenarios, no file edits needed
2. **App.config** - Persisted settings, requires app restart after changes

Both methods work identically. This guide uses environment variables for convenience during iterative testing.

## Test Scenarios

The following scenarios use ServiceControl (Primary) as an example. To test other instances, use the appropriate environment variable prefix and port.

> **Important:** Set environment variables in the same terminal where you run `dotnet run`. Environment variables are scoped to the terminal session.
>
> **Tip:** Check the application startup logs to verify which settings were applied. The authentication configuration is logged at startup.

### Scenario 1: Authentication Disabled (Default)

Test the default behavior where authentication is disabled and all requests are allowed.

**Clear environment variables and start ServiceControl:**

```cmd
set SERVICECONTROL_AUTHENTICATION_ENABLED=
set SERVICECONTROL_AUTHENTICATION_AUTHORITY=
set SERVICECONTROL_AUTHENTICATION_AUDIENCE=
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_CLIENTID=
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_APISCOPES=
set SERVICECONTROL_AUTHENTICATION_REQUIREHTTPSMETADATA=
set SERVICECONTROL_AUTHENTICATION_VALIDATEISSUER=
set SERVICECONTROL_AUTHENTICATION_VALIDATEAUDIENCE=

cd src\ServiceControl
dotnet run
```

**Test with curl (no authorization header):**

```cmd
curl http://localhost:33333/api | json
```

**Expected output:**

```json
{
  "description": "The management backend for the Particular Service Platform",
  ...
}
```

Requests succeed without authentication because `Authentication.Enabled` defaults to `false`.

**Check authentication configuration endpoint:**

```cmd
curl http://localhost:33333/api/authentication/configuration | json
```

**Expected output:**

```json
{
  "enabled": false
}
```

The configuration indicates authentication is disabled. Other fields are omitted when null.

### Scenario 2: Authentication Enabled (No Token)

Test that requests without a token are rejected when authentication is enabled.

> **Note:** This scenario requires a valid OIDC authority URL. For testing authentication enforcement without a real provider, you can use any HTTP URL - the request will fail before token validation because no token is provided.

**Clear environment variables and start ServiceControl:**

```cmd
set SERVICECONTROL_AUTHENTICATION_ENABLED=true
set SERVICECONTROL_AUTHENTICATION_AUTHORITY=https://login.microsoftonline.com/common/v2.0
set SERVICECONTROL_AUTHENTICATION_AUDIENCE=api://servicecontrol-test
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_CLIENTID=test-client-id
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_APISCOPES=["api://servicecontrol-test/.default"]
set SERVICECONTROL_AUTHENTICATION_REQUIREHTTPSMETADATA=
set SERVICECONTROL_AUTHENTICATION_VALIDATEISSUER=
set SERVICECONTROL_AUTHENTICATION_VALIDATEAUDIENCE=

cd src\ServiceControl
dotnet run
```

**Test with curl (no authorization header):**

```cmd
curl -v http://localhost:33333/api/endpoints 2>&1 | findstr /C:"HTTP/"
```

**Expected output:**

```text
< HTTP/1.1 401 Unauthorized
```

Requests without a token are rejected with `401 Unauthorized`.

> **Note:** The `/api` root endpoint and `/api/authentication/configuration` are marked as anonymous and will return 200 OK even with authentication enabled. Test protected endpoints like `/api/endpoints` to verify authentication enforcement.

**Check authentication configuration endpoint (no auth required):**

```cmd
curl http://localhost:33333/api/authentication/configuration | json
```

**Expected output:**

```json
{
  "enabled": true,
  "clientId": "test-client-id",
  "audience": "api://servicecontrol-test",
  "apiScopes": "[\"api://servicecontrol-test/.default\"]"
}
```

The authentication configuration endpoint is accessible without authentication and returns the configuration that clients need to authenticate. The `authority` field is omitted when `ServicePulse.Authority` is not explicitly set (it defaults to the main Authority for ServicePulse clients).

### Scenario 3: Authentication with Invalid Token

Test that requests with an invalid token are rejected.

**Start ServiceControl with authentication enabled (same as Scenario 2):**

```cmd
set SERVICECONTROL_AUTHENTICATION_ENABLED=true
set SERVICECONTROL_AUTHENTICATION_AUTHORITY=https://login.microsoftonline.com/common/v2.0
set SERVICECONTROL_AUTHENTICATION_AUDIENCE=api://servicecontrol-test
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_CLIENTID=test-client-id
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_APISCOPES=["api://servicecontrol-test/.default"]
set SERVICECONTROL_AUTHENTICATION_REQUIREHTTPSMETADATA=
set SERVICECONTROL_AUTHENTICATION_VALIDATEISSUER=
set SERVICECONTROL_AUTHENTICATION_VALIDATEAUDIENCE=

cd src\ServiceControl
dotnet run
```

**Test with curl (invalid token):**

```cmd
curl -v -H "Authorization: Bearer invalid-token-here" http://localhost:33333/api/endpoints 2>&1 | findstr /C:"HTTP/"
```

**Expected output:**

```text
< HTTP/1.1 401 Unauthorized
```

Invalid tokens are rejected with `401 Unauthorized`.

### Scenario 4: Anonymous Endpoints

Test that anonymous endpoints remain accessible when authentication is enabled.

**With ServiceControl still running from Scenario 2 or 3, test anonymous endpoints:**

```cmd
curl http://localhost:33333/api | json
```

**Expected output:**

```json
{
  "description": "The management backend for the Particular Service Platform",
  ...
}
```

```cmd
curl http://localhost:33333/api/authentication/configuration | json
```

**Expected output:**

```json
{
  "enabled": true,
  "clientId": "test-client-id",
  "audience": "api://servicecontrol-test",
  "apiScopes": "[\"api://servicecontrol-test/.default\"]"
}
```

The following endpoints are marked as anonymous and accessible without authentication:

| Endpoint | Purpose |
|----------|---------|
| `/api` | API root/discovery - returns available endpoints |
| `/api/authentication/configuration` | Returns auth config for clients like ServicePulse |

### Scenario 5: Validation Settings Warnings

Test that disabling validation settings produces warnings in the logs.

**Start ServiceControl with relaxed validation:**

```cmd
set SERVICECONTROL_AUTHENTICATION_ENABLED=true
set SERVICECONTROL_AUTHENTICATION_AUTHORITY=https://login.microsoftonline.com/common/v2.0
set SERVICECONTROL_AUTHENTICATION_AUDIENCE=api://servicecontrol-test
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_CLIENTID=test-client-id
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_APISCOPES=["api://servicecontrol-test/.default"]
set SERVICECONTROL_AUTHENTICATION_REQUIREHTTPSMETADATA=
set SERVICECONTROL_AUTHENTICATION_VALIDATEISSUER=false
set SERVICECONTROL_AUTHENTICATION_VALIDATEAUDIENCE=false

cd src\ServiceControl
dotnet run
```

**Expected log output:**

```text
warn: Authentication.ValidateIssuer is set to false. This is not recommended for production environments...
warn: Authentication.ValidateAudience is set to false. This is not recommended for production environments...
```

The application warns about insecure validation settings.

### Scenario 6: Missing Required Settings

Test that missing required settings prevent startup.

**Start ServiceControl with missing authority:**

```cmd
set SERVICECONTROL_AUTHENTICATION_ENABLED=true
set SERVICECONTROL_AUTHENTICATION_AUTHORITY=
set SERVICECONTROL_AUTHENTICATION_AUDIENCE=api://servicecontrol-test
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_CLIENTID=test-client-id
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_APISCOPES=["api://servicecontrol-test/.default"]
set SERVICECONTROL_AUTHENTICATION_REQUIREHTTPSMETADATA=
set SERVICECONTROL_AUTHENTICATION_VALIDATEISSUER=
set SERVICECONTROL_AUTHENTICATION_VALIDATEAUDIENCE=

cd src\ServiceControl
dotnet run
```

**Expected behavior:**

The application fails to start with an error message:

```text
Authentication.Authority is required when authentication is enabled. Please provide a valid OpenID Connect authority URL...
```

### Scenario 7: Authentication with Valid Token (Real Identity Provider)

Test end-to-end authentication with a valid token from a real OIDC provider.

> **Prerequisites:** This scenario requires a configured OIDC provider (e.g., Microsoft Entra ID, Auth0, Okta).

**Microsoft Entra ID Setup (one-time):**

1. **Create an App Registration** for ServiceControl API:
   - Go to Azure Portal > Microsoft Entra ID > App registrations
   - Create a new registration (e.g., "ServiceControl API")
   - Note the Application (client) ID and Directory (tenant) ID
   - Under "Expose an API", add a scope (e.g., `access_as_user`)

2. **Create an App Registration** for testing (or use ServicePulse's):
   - Create another registration for the client application
   - Under "API permissions", add permission to your ServiceControl API scope
   - Under "Authentication", enable "Allow public client flows" for testing

**Start ServiceControl with your Entra ID configuration:**

```cmd
set SERVICECONTROL_AUTHENTICATION_ENABLED=true
set SERVICECONTROL_AUTHENTICATION_AUTHORITY=https://login.microsoftonline.com/{tenant-id}/v2.0
set SERVICECONTROL_AUTHENTICATION_AUDIENCE=api://servicecontrol
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_CLIENTID={servicepulse-client-id}
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_APISCOPES=["api://servicecontrol/access_as_user"]
set SERVICECONTROL_AUTHENTICATION_REQUIREHTTPSMETADATA=
set SERVICECONTROL_AUTHENTICATION_VALIDATEISSUER=
set SERVICECONTROL_AUTHENTICATION_VALIDATEAUDIENCE=

cd src\ServiceControl
dotnet run
```

**Get a test token using Azure CLI:**

```cmd
az login
az account get-access-token --resource api://servicecontrol --query accessToken -o tsv
```

**Test with the token:**

```cmd
curl -H "Authorization: Bearer {token}" http://localhost:33333/api/endpoints | json
```

**Expected output:**

```json
[]
```

Requests with a valid token are processed successfully. The response will be an empty array if no endpoints are registered, or a list of endpoints if data exists.

## Testing Other Instances

The scenarios above use ServiceControl (Primary). To test ServiceControl.Audit or ServiceControl.Monitoring:

1. Use the appropriate environment variable prefix (see Instance Reference above)
2. Use the corresponding project directory and port
3. Note: Audit and Monitoring instances don't require ServicePulse settings

| Instance | Project Directory | Port | Env Var Prefix |
|----------|-------------------|------|----------------|
| ServiceControl (Primary) | `src\ServiceControl` | 33333 | `SERVICECONTROL_` |
| ServiceControl.Audit | `src\ServiceControl.Audit` | 44444 | `SERVICECONTROL_AUDIT_` |
| ServiceControl.Monitoring | `src\ServiceControl.Monitoring` | 33633 | `MONITORING_` |

## Cleanup

After testing, clear the environment variables:

**Command Prompt (cmd):**

```cmd
set SERVICECONTROL_AUTHENTICATION_ENABLED=
set SERVICECONTROL_AUTHENTICATION_AUTHORITY=
set SERVICECONTROL_AUTHENTICATION_AUDIENCE=
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_CLIENTID=
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_APISCOPES=
set SERVICECONTROL_AUTHENTICATION_VALIDATEISSUER=
set SERVICECONTROL_AUTHENTICATION_VALIDATEAUDIENCE=
set SERVICECONTROL_AUTHENTICATION_VALIDATELIFETIME=
set SERVICECONTROL_AUTHENTICATION_VALIDATEISSUERSIGNINGKEY=
set SERVICECONTROL_AUTHENTICATION_REQUIREHTTPSMETADATA=
```

**PowerShell:**

```powershell
$env:SERVICECONTROL_AUTHENTICATION_ENABLED = $null
$env:SERVICECONTROL_AUTHENTICATION_AUTHORITY = $null
$env:SERVICECONTROL_AUTHENTICATION_AUDIENCE = $null
$env:SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_CLIENTID = $null
$env:SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_APISCOPES = $null
$env:SERVICECONTROL_AUTHENTICATION_VALIDATEISSUER = $null
$env:SERVICECONTROL_AUTHENTICATION_VALIDATEAUDIENCE = $null
$env:SERVICECONTROL_AUTHENTICATION_VALIDATELIFETIME = $null
$env:SERVICECONTROL_AUTHENTICATION_VALIDATEISSUERSIGNINGKEY = $null
$env:SERVICECONTROL_AUTHENTICATION_REQUIREHTTPSMETADATA = $null
```

## See Also

- [Authentication Configuration](authentication.md) - Configuration reference for authentication settings
- [HTTPS Configuration](https-configuration.md) - HTTPS is recommended when authentication is enabled
- [Local Forwarded Headers Testing](local-forward-headers-testing.md) - Testing forwarded headers
