# Local Authentication Testing

This guide explains how to test authentication configuration for ServiceControl instances. This approach uses curl to test authentication enforcement and configuration endpoints.

## Prerequisites

- ServiceControl built locally (see [main README for instructions](../README.md#how-to-rundebug-locally))
- **Identity Provider (IdP) configured** - For real authentication testing (Scenarios 7+), you need an OIDC provider configured with:
  - An API application registration (for ServiceControl)
  - A client application registration (for ServicePulse)
  - API scopes configured and permissions granted
  - See [ServiceControl Authentication](https://docs.particular.net/servicecontrol/security/configuration/authentication) for example setups
- curl (included with Windows 10/11, Git Bash, or WSL)
- HTTP Request logging to view comms to and from instances
- (Optional) For formatted JSON output: `npm install -g json` then pipe curl output through `| json`

## Enabling Debug Logs

To enable detailed logging for troubleshooting, set the `LogLevel` environment variable before starting each instance:

```cmd
set SERVICECONTROL_LOGLEVEL=Debug
set SERVICECONTROL_AUDIT_LOGLEVEL=Debug
set MONITORING_LOGLEVEL=Debug
```

**Valid log levels:** `Trace`, `Debug`, `Information` (or `Info`), `Warning` (or `Warn`), `Error`, `Critical` (or `Fatal`), `None` (or `Off`)

Debug logs will show detailed authentication flow information including token validation, claims processing, and authorization decisions.

### HTTP Request Logs

HTTP logs can be enabled by adding a `nlog.config` file in beside the exe:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<nlog xmlns="http://www.nlog-project.org/schemas/NLog.xsd"
      xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  
  <targets>
    <target name="console" xsi:type="ColoredConsole" 
            layout="${longdate}|${level}|${logger}|${message}${onexception:|${exception:format=tostring}}" />
  </targets>

  <rules>
    <!-- Enable HTTP logging -->
    <logger name="Microsoft.AspNetCore.HttpLogging.*" minlevel="Info" writeTo="console" />
    
    <!-- Suppress other ASP.NET Core noise -->
    <logger name="Microsoft.AspNetCore.*" maxlevel="Info" final="true" />
    
    <!-- Everything else -->
    <logger name="*" minlevel="Info" writeTo="console" />
  </rules>
</nlog>
```

## Instance Reference

| Instance                  | Project Directory               | Default Port | Environment Variable Prefix |
|---------------------------|---------------------------------|--------------|-----------------------------|
| ServiceControl (Primary)  | `src\ServiceControl`            | 33333        | `SERVICECONTROL_`           |
| ServiceControl.Audit      | `src\ServiceControl.Audit`      | 44444        | `SERVICECONTROL_AUDIT_`     |
| ServiceControl.Monitoring | `src\ServiceControl.Monitoring` | 33633        | `MONITORING_`               |

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

> [!IMPORTANT]
> Set environment variables in the same terminal where you run `dotnet run`. Environment variables are scoped to the terminal session.
> Check the application startup logs to verify which settings were applied. The authentication configuration is logged at startup.

### Test Grouping by Configuration

To minimize service restarts during testing, scenarios are grouped by configuration. Run all tests within a group before changing configuration:

| Configuration Group                        | Scenarios        | Description                                        |
|--------------------------------------------|------------------|----------------------------------------------------|
| **Group A**: Auth Disabled                 | 1                | Default configuration with authentication disabled |
| **Group B**: Auth Enabled (Test Authority) | 2, 3, 4          | Authentication enabled with test authority values  |
| **Group C**: Relaxed Validation            | 5                | Authentication with validation warnings            |
| **Group D**: Missing Settings              | 6                | Startup failure test (missing required settings)   |
| **Group E**: Real IdP (Full Setup)         | 7, 8, 10, 11, 14 | Real identity provider with scatter-gather tests   |
| **Group F**: Mismatched Audiences          | 9                | Primary and Audit with different audience settings |
| **Group G**: Mixed (Primary Only Auth)     | 12               | Primary has auth, Audit does not                   |
| **Group H**: Mixed (Remotes Only Auth)     | 13               | Audit has auth, Primary does not                   |

---

## Group A: Authentication Disabled Configuration

**Start the instance once (Scenario 1).**

```cmd
rem ServiceControl (Primary)
set SERVICECONTROL_AUTHENTICATION_ENABLED=
set SERVICECONTROL_AUTHENTICATION_AUTHORITY=
set SERVICECONTROL_AUTHENTICATION_AUDIENCE=
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_CLIENTID=
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_AUTHORITY=
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_APISCOPES=
set SERVICECONTROL_AUTHENTICATION_REQUIREHTTPSMETADATA=
set SERVICECONTROL_AUTHENTICATION_VALIDATEISSUER=
set SERVICECONTROL_AUTHENTICATION_VALIDATEAUDIENCE=

rem ServiceControl.Audit
set SERVICECONTROL_AUDIT_AUTHENTICATION_ENABLED=
set SERVICECONTROL_AUDIT_AUTHENTICATION_AUTHORITY=
set SERVICECONTROL_AUDIT_AUTHENTICATION_AUDIENCE=
set SERVICECONTROL_AUDIT_AUTHENTICATION_REQUIREHTTPSMETADATA=
set SERVICECONTROL_AUDIT_AUTHENTICATION_VALIDATEISSUER=
set SERVICECONTROL_AUDIT_AUTHENTICATION_VALIDATEAUDIENCE=

rem ServiceControl.Monitoring
set MONITORING_AUTHENTICATION_ENABLED=
set MONITORING_AUTHENTICATION_AUTHORITY=
set MONITORING_AUTHENTICATION_AUDIENCE=
set MONITORING_AUTHENTICATION_REQUIREHTTPSMETADATA=
set MONITORING_AUTHENTICATION_VALIDATEISSUER=
set MONITORING_AUTHENTICATION_VALIDATEAUDIENCE=

dotnet run
```

### Scenario 1: Authentication Disabled (Default)

Test the default behavior where authentication is disabled and all requests are allowed.

#### Test with curl (no authorization header)

```cmd
rem ServiceControl (Primary)
curl http://localhost:33333/api | json

rem ServiceControl.Audit
curl http://localhost:44444/api | json

rem ServiceControl.Monitoring
curl http://localhost:33633/ | json
```

**Expected output:**

```json
{
  "description": "The management backend for the Particular Service Platform", // or "description": "The audit backend for the Particular Service Platform" or "instanceType": "monitoring",
  ...
}
```

Requests succeed without authentication because `Authentication.Enabled` defaults to `false`.

#### Check authentication configuration endpoint

```cmd
rem ServiceControl (Primary)
curl http://localhost:33333/api/authentication/configuration | json

rem ServiceControl.Audit
curl http://localhost:44444/api/authentication/configuration | json

rem ServiceControl.Monitoring
curl http://localhost:33633/api/authentication/configuration | json
```

**Expected output:** (Only for the primary instance)

```json
{
  "enabled": false
}
```

The configuration indicates authentication is disabled. Other fields are omitted when null.

---

## Group B: Authentication Enabled (Test Authority) Configuration

**Restart the instance with this configuration, then run all tests in this group (Scenarios 2, 3, 4).**

```cmd
rem ServiceControl (Primary)
set SERVICECONTROL_AUTHENTICATION_ENABLED=true
set SERVICECONTROL_AUTHENTICATION_AUTHORITY=https://login.microsoftonline.com/common/v2.0
set SERVICECONTROL_AUTHENTICATION_AUDIENCE=api://servicecontrol-test
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_CLIENTID=test-client-id
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_AUTHORITY=https://login.microsoftonline.com/common/v2.0
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_APISCOPES=["api://servicecontrol-test/access_as_user"]
set SERVICECONTROL_AUTHENTICATION_REQUIREHTTPSMETADATA=
set SERVICECONTROL_AUTHENTICATION_VALIDATEISSUER=
set SERVICECONTROL_AUTHENTICATION_VALIDATEAUDIENCE=

rem ServiceControl.Audit
set SERVICECONTROL_AUDIT_AUTHENTICATION_ENABLED=true
set SERVICECONTROL_AUDIT_AUTHENTICATION_AUTHORITY=https://login.microsoftonline.com/common/v2.0
set SERVICECONTROL_AUDIT_AUTHENTICATION_AUDIENCE=api://servicecontrol-test
set SERVICECONTROL_AUDIT_AUTHENTICATION_REQUIREHTTPSMETADATA=
set SERVICECONTROL_AUDIT_AUTHENTICATION_VALIDATEISSUER=
set SERVICECONTROL_AUDIT_AUTHENTICATION_VALIDATEAUDIENCE=

rem ServiceControl.Monitoring
set MONITORING_AUTHENTICATION_ENABLED=true
set MONITORING_AUTHENTICATION_AUTHORITY=https://login.microsoftonline.com/common/v2.0
set MONITORING_AUTHENTICATION_AUDIENCE=api://servicecontrol-test
set MONITORING_AUTHENTICATION_REQUIREHTTPSMETADATA=
set MONITORING_AUTHENTICATION_VALIDATEISSUER=
set MONITORING_AUTHENTICATION_VALIDATEAUDIENCE=

dotnet run
```

> [!NOTE]
> This configuration uses a test authority URL. For testing authentication enforcement without a real provider, any HTTP URL works - requests fail before token validation because no valid token is provided.

### Scenario 2: Authentication Enabled (No Token)

Test that requests without a token are rejected when authentication is enabled.

#### Test protected endpoint without token

```cmd
rem ServiceControl (Primary)
curl -v http://localhost:33333/api/endpoints 2>&1 | findstr /C:"HTTP/"

rem ServiceControl.Audit
curl -v http://localhost:44444/api/messages 2>&1 | findstr /C:"HTTP/"

rem ServiceControl.Monitoring
curl -v http://localhost:33633/monitored-endpoints 2>&1 | findstr /C:"HTTP/"
```

**Expected output:**

```text
...
< HTTP/1.1 401 Unauthorized
```

Requests without a token are rejected with `401 Unauthorized`.

> [!NOTE]
> The endpoint `/api/authentication/configuration` are marked as anonymous and will return `200 OK` even with authentication enabled. Test protected endpoints like `/api/endpoints` to verify authentication enforcement.

#### Check authentication configuration endpoint (no auth required)

> [!NOTE]
> Only the primary instance has this endpoint. Requesting this endpoint from the audit and monitoring instance will return unauthorized.

```cmd
rem ServiceControl (Primary)
curl http://localhost:33333/api/authentication/configuration | json

rem ServiceControl.Audit
curl http://localhost:44444/api/authentication/configuration | json

rem ServiceControl.Monitoring
curl http://localhost:33633/api/authentication/configuration | json
```

**Expected output:**

```json
{
  "enabled": true,
  "clientId": "test-client-id",
  "audience": "api://servicecontrol-test",
  "apiScopes": "[\"api://servicecontrol-test/access_as_user\"]"
}
```

The authentication configuration endpoint is accessible without authentication and returns the configuration that clients need to authenticate. The `authority` field is omitted when `ServicePulse.Authority` is not explicitly set (it defaults to the main Authority for ServicePulse clients). The `audience` field is copied from the `ServiceControl/Authentication.Audience` value.

### Scenario 3: Authentication with Invalid Token

Test that requests with an invalid token are rejected.

#### Test with curl (using Group B configuration above, invalid token)

```cmd
rem ServiceControl (Primary)
curl -v -H "Authorization: Bearer invalid-token-here" http://localhost:33333/api/endpoints 2>&1 | findstr /C:"HTTP/"

rem ServiceControl.Audit
curl -v -H "Authorization: Bearer invalid-token-here" http://localhost:44444/api/messages 2>&1 | findstr /C:"HTTP/"

rem ServiceControl.Monitoring
curl -v -H "Authorization: Bearer invalid-token-here" http://localhost:33633/monitored-endpoints 2>&1 | findstr /C:"HTTP/"
```

**Expected output:**

```text
...
< HTTP/1.1 401 Unauthorized
```

Invalid tokens are rejected with `401 Unauthorized`.

### Scenario 4: Anonymous Endpoints

Test that anonymous endpoints remain accessible when authentication is enabled.

#### Test with curl (using Group B configuration above)

```cmd
rem ServiceControl (Primary)
curl http://localhost:33333/api | json

rem ServiceControl.Audit
curl http://localhost:44444/api | json

rem ServiceControl.Monitoring
curl http://localhost:33633/ | json
```

**Expected output:**

```json
{
  "description": "The management backend for the Particular Service Platform", // or "description": "The audit backend for the Particular Service Platform", or "instanceType": "monitoring",
  ...
}
```

See [Authentication](https://docs.particular.net/servicecontrol/security/#authentication-anonymous-endpoints) for all anonymous endpoints.

---

## Group C: Relaxed Validation Configuration

**Restart the instance with this configuration (Scenario 5).**

```cmd
rem ServiceControl (Primary)
set SERVICECONTROL_AUTHENTICATION_ENABLED=true
set SERVICECONTROL_AUTHENTICATION_AUTHORITY=https://login.microsoftonline.com/common/v2.0
set SERVICECONTROL_AUTHENTICATION_AUDIENCE=api://servicecontrol-test
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_CLIENTID=test-client-id
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_AUTHORITY=https://login.microsoftonline.com/common/v2.0
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_APISCOPES=["api://servicecontrol-test/access_as_user"]
set SERVICECONTROL_AUTHENTICATION_REQUIREHTTPSMETADATA=
set SERVICECONTROL_AUTHENTICATION_VALIDATEISSUER=false
set SERVICECONTROL_AUTHENTICATION_VALIDATEAUDIENCE=false

rem ServiceControl.Audit
set SERVICECONTROL_AUDIT_AUTHENTICATION_ENABLED=true
set SERVICECONTROL_AUDIT_AUTHENTICATION_AUTHORITY=https://login.microsoftonline.com/common/v2.0
set SERVICECONTROL_AUDIT_AUTHENTICATION_AUDIENCE=api://servicecontrol-test
set SERVICECONTROL_AUDIT_AUTHENTICATION_REQUIREHTTPSMETADATA=
set SERVICECONTROL_AUDIT_AUTHENTICATION_VALIDATEISSUER=false
set SERVICECONTROL_AUDIT_AUTHENTICATION_VALIDATEAUDIENCE=false

rem ServiceControl.Monitoring
set MONITORING_AUTHENTICATION_ENABLED=true
set MONITORING_AUTHENTICATION_AUTHORITY=https://login.microsoftonline.com/common/v2.0
set MONITORING_AUTHENTICATION_AUDIENCE=api://servicecontrol-test
set MONITORING_AUTHENTICATION_REQUIREHTTPSMETADATA=
set MONITORING_AUTHENTICATION_VALIDATEISSUER=false
set MONITORING_AUTHENTICATION_VALIDATEAUDIENCE=false

dotnet run
```

### Scenario 5: Validation Settings Warnings

Test that disabling validation settings produces warnings in the logs.

**Expected log output:**

```text
warn: Authentication.ValidateIssuer is disabled. Tokens from any issuer will be accepted. It is recommended to keep this enabled for security
warn: Authentication.ValidateAudience is disabled. Tokens intended for other applications will be accepted. It is recommended to keep this enabled for security
```

The application warns about insecure validation settings.

---

## Group D: Missing Settings Configuration (Startup Failure Test)

**Attempt to start the instance with this configuration (Scenario 6). The instance should fail to start.**

```cmd
rem ServiceControl (Primary)
set SERVICECONTROL_AUTHENTICATION_ENABLED=true
set SERVICECONTROL_AUTHENTICATION_AUTHORITY=
set SERVICECONTROL_AUTHENTICATION_AUDIENCE=api://servicecontrol-test
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_CLIENTID=test-client-id
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_AUTHORITY=
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_APISCOPES=["api://servicecontrol-test/access_as_user"]
set SERVICECONTROL_AUTHENTICATION_REQUIREHTTPSMETADATA=
set SERVICECONTROL_AUTHENTICATION_VALIDATEISSUER=
set SERVICECONTROL_AUTHENTICATION_VALIDATEAUDIENCE=

rem ServiceControl.Audit
set SERVICECONTROL_AUDIT_AUTHENTICATION_ENABLED=true
set SERVICECONTROL_AUDIT_AUTHENTICATION_AUTHORITY=
set SERVICECONTROL_AUDIT_AUTHENTICATION_AUDIENCE=api://servicecontrol-test
set SERVICECONTROL_AUDIT_AUTHENTICATION_REQUIREHTTPSMETADATA=
set SERVICECONTROL_AUDIT_AUTHENTICATION_VALIDATEISSUER=
set SERVICECONTROL_AUDIT_AUTHENTICATION_VALIDATEAUDIENCE=

rem ServiceControl.Monitoring
set MONITORING_AUTHENTICATION_ENABLED=true
set MONITORING_AUTHENTICATION_AUTHORITY=
set MONITORING_AUTHENTICATION_AUDIENCE=api://servicecontrol-test
set MONITORING_AUTHENTICATION_REQUIREHTTPSMETADATA=
set MONITORING_AUTHENTICATION_VALIDATEISSUER=
set MONITORING_AUTHENTICATION_VALIDATEAUDIENCE=

dotnet run
```

### Scenario 6: Missing Required Settings

Test that missing required settings prevent startup.

**Expected behavior:**

The application fails to start with an error message:

```text
Authentication.Authority is required when authentication is enabled. Please provide a valid OpenID Connect authority URL...
```

---

## Group E: Real Identity Provider Configuration

> [!IMPORTANT]
> This group requires a configured OIDC provider (e.g., Microsoft Entra ID, Auth0, Okta).
> See [ServiceControl Authentication](https://docs.particular.net/servicecontrol/security/configuration/authentication) for setup examples.

**Start all instances with this configuration, then run all tests in this group (Scenarios 7, 8, 10, 11, 14).**

> [!NOTE]
> See [HTTPS Testing](https-testing.md) for certificate setup instructions using mkcert.

```cmd
rem ServiceControl (Primary)
set SERVICECONTROL_HTTPS_ENABLED=true
set SERVICECONTROL_HTTPS_CERTIFICATEPATH=C:\path\to\cert\cert.pfx
set SERVICECONTROL_HTTPS_CERTIFICATEPASSWORD=changeit
set SERVICECONTROL_AUTHENTICATION_ENABLED=true
set SERVICECONTROL_AUTHENTICATION_AUTHORITY=https://login.microsoftonline.com/{tenantId}
set SERVICECONTROL_AUTHENTICATION_AUDIENCE=api://{servicecontrol-audience}
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_CLIENTID={servicepulse-clientid}
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_AUTHORITY=https://login.microsoftonline.com/{tenantId}/v2.0
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_APISCOPES=["api://{servicecontrol-audience}/access_as_user"]
set SERVICECONTROL_AUTHENTICATION_REQUIREHTTPSMETADATA=
set SERVICECONTROL_AUTHENTICATION_VALIDATEISSUER=
set SERVICECONTROL_AUTHENTICATION_VALIDATEAUDIENCE=
set SERVICECONTROL_REMOTEINSTANCES=[{"api_uri":"https://localhost:44444"}]

rem ServiceControl.Audit
set SERVICECONTROL_AUDIT_HTTPS_ENABLED=true
set SERVICECONTROL_AUDIT_HTTPS_CERTIFICATEPATH=C:\path\to\cert\cert.pfx
set SERVICECONTROL_AUDIT_HTTPS_CERTIFICATEPASSWORD=changeit
set SERVICECONTROL_AUDIT_AUTHENTICATION_ENABLED=true
set SERVICECONTROL_AUDIT_AUTHENTICATION_AUTHORITY=https://login.microsoftonline.com/{tenantId}
set SERVICECONTROL_AUDIT_AUTHENTICATION_AUDIENCE=api://{servicecontrol-audience}
set SERVICECONTROL_AUDIT_AUTHENTICATION_REQUIREHTTPSMETADATA=
set SERVICECONTROL_AUDIT_AUTHENTICATION_VALIDATEISSUER=
set SERVICECONTROL_AUDIT_AUTHENTICATION_VALIDATEAUDIENCE=

rem ServiceControl.Monitoring
set MONITORING_HTTPS_ENABLED=true
set MONITORING_HTTPS_CERTIFICATEPATH=C:\path\to\cert\cert.pfx
set MONITORING_HTTPS_CERTIFICATEPASSWORD=changeit
set MONITORING_AUTHENTICATION_ENABLED=true
set MONITORING_AUTHENTICATION_AUTHORITY=https://login.microsoftonline.com/{tenantId}
set MONITORING_AUTHENTICATION_AUDIENCE=api://{servicecontrol-audience}
set MONITORING_AUTHENTICATION_REQUIREHTTPSMETADATA=
set MONITORING_AUTHENTICATION_VALIDATEISSUER=
set MONITORING_AUTHENTICATION_VALIDATEAUDIENCE=

dotnet run
```

### Scenario 7: Authentication with Valid Token (Real Identity Provider)

Test end-to-end authentication with a valid token from a real OIDC provider.

#### Get a test token using Azure CLI

```cmd
az login
set TOKEN=$(az account get-access-token --resource api://servicecontrol --query accessToken -o tsv)
```

#### Test with the token

```cmd
rem ServiceControl (Primary)
curl --ssl-no-revoke -H "Authorization: Bearer %TOKEN%" https://localhost:33333/api/endpoints | json

rem ServiceControl.Audit
curl --ssl-no-revoke -H "Authorization: Bearer %TOKEN%" https://localhost:44444/api/messages | json

rem ServiceControl.Monitoring
curl --ssl-no-revoke -H "Authorization: Bearer %TOKEN%" https://localhost:33633/monitored-endpoints | json
```

**Expected output:**

```json
[]
```

Requests with a valid token are processed successfully. The response will be an empty array if no data exists, or a list of items if data exists.

### Scenario 8: Scatter-Gather with Authentication (Token Forwarding)

Test that the primary instance forwards authentication tokens to remote instances during scatter-gather operations.

> [!NOTE]
> When a client queries endpoints like `/api/messages`, the primary instance may query remote Audit instances to aggregate results. The client's authorization token is forwarded to these remote instances.

#### Get a test token and query the primary instance (using Group E configuration above)

```cmd
az login
set TOKEN=$(az account get-access-token --resource api://servicecontrol --query accessToken -o tsv)

curl --ssl-no-revoke -H "Authorization: Bearer %TOKEN%" https://localhost:33333/api/messages | json
```

Ensure `Debug` logs are enabled. Take a look at the primary and audit logs. You should see the requests being sent/received indicating if an auth header is included.

#### Test with no token (should fail)

```cmd
curl --ssl-no-revoke -v https://localhost:33333/api/messages 2>&1 | findstr /C:"HTTP/"
```

**Expected output:**

No audit logs, and:

```text
< HTTP/1.1 401 Unauthorized
```

### Scenario 10: Remote Instance Health Checks with Authentication

Test that the primary instance can check remote instance health when authentication is enabled.

> [!NOTE]
> The health check queries the `/api` endpoint on remote instances. This endpoint is marked as anonymous and should be accessible without authentication.

#### Check the remote instances configuration endpoint

```cmd
curl --ssl-no-revoke -H "Authorization: Bearer %TOKEN%" https://localhost:33333/api/configuration/remotes | json
```

**Expected output:**

You should see a log in the audit instance stating a request was received at the `/api/configuration` endpoint, and that no auth header was included.

```json
[
  {
    "api_uri": "https://localhost:44444",
    "status": "online",
    "version": "5.x.x"
    ...
  }
]
```

The health check should succeed because `/api` is an anonymous endpoint.

### Scenario 11: Platform Connection Details with Authentication

Test that platform connection details can be retrieved when authentication is enabled on remote instances.

> [!NOTE]
> The primary instance queries `/api/connection` on remote instances to aggregate platform connection details. This endpoint requires authentication.

#### Test connection endpoint

```cmd
curl --ssl-no-revoke -H "Authorization: Bearer %TOKEN%" https://localhost:33333/api/connection | json
```

**Expected behavior:**

The platform connection response includes connection details from both the primary and remote instances. The audit log will show the request.

### Scenario 14: Expired Token Forwarding

Test how scatter-gather handles expired tokens being forwarded to remote instances.

#### Use an expired token

```cmd
curl --ssl-no-revoke -v -H "Authorization: Bearer {expired-token}" https://localhost:33333/api/messages 2>&1 | findstr /C:"HTTP/"
```

**Expected output:**

```text
< HTTP/1.1 401 Unauthorized
```

The primary instance rejects the expired token before any remote requests are made.

---

## Group F: Mismatched Audiences Configuration

**Restart all instances with this configuration (Scenario 9). Note the DIFFERENT audience for Audit.**

> [!NOTE]
> See [HTTPS Testing](https-testing.md) for certificate setup instructions using mkcert.

```cmd
rem ServiceControl (Primary)
set SERVICECONTROL_HTTPS_ENABLED=true
set SERVICECONTROL_HTTPS_CERTIFICATEPATH=C:\path\to\cert\cert.pfx
set SERVICECONTROL_HTTPS_CERTIFICATEPASSWORD=changeit
set SERVICECONTROL_AUTHENTICATION_ENABLED=true
set SERVICECONTROL_AUTHENTICATION_AUTHORITY=https://login.microsoftonline.com/{tenantId}
set SERVICECONTROL_AUTHENTICATION_AUDIENCE=api://{servicecontrol-audience}
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_CLIENTID={servicepulse-clientid}
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_AUTHORITY=https://login.microsoftonline.com/{tenantId}/v2.0
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_APISCOPES=["api://{servicecontrol-audience}/access_as_user"]
set SERVICECONTROL_AUTHENTICATION_REQUIREHTTPSMETADATA=
set SERVICECONTROL_AUTHENTICATION_VALIDATEISSUER=
set SERVICECONTROL_AUTHENTICATION_VALIDATEAUDIENCE=
set SERVICECONTROL_REMOTEINSTANCES=[{"api_uri":"https://localhost:44444"}]

rem ServiceControl.Audit (DIFFERENT audience)
set SERVICECONTROL_AUDIT_HTTPS_ENABLED=true
set SERVICECONTROL_AUDIT_HTTPS_CERTIFICATEPATH=C:\path\to\cert\cert.pfx
set SERVICECONTROL_AUDIT_HTTPS_CERTIFICATEPASSWORD=changeit
set SERVICECONTROL_AUDIT_AUTHENTICATION_ENABLED=true
set SERVICECONTROL_AUDIT_AUTHENTICATION_AUTHORITY=https://login.microsoftonline.com/{tenantId}
set SERVICECONTROL_AUDIT_AUTHENTICATION_AUDIENCE=api://servicecontrol-audit-different
set SERVICECONTROL_AUDIT_AUTHENTICATION_REQUIREHTTPSMETADATA=
set SERVICECONTROL_AUDIT_AUTHENTICATION_VALIDATEISSUER=
set SERVICECONTROL_AUDIT_AUTHENTICATION_VALIDATEAUDIENCE=

rem ServiceControl.Monitoring
set MONITORING_HTTPS_ENABLED=true
set MONITORING_HTTPS_CERTIFICATEPATH=C:\path\to\cert\cert.pfx
set MONITORING_HTTPS_CERTIFICATEPASSWORD=changeit
set MONITORING_AUTHENTICATION_ENABLED=true
set MONITORING_AUTHENTICATION_AUTHORITY=https://login.microsoftonline.com/{tenantId}
set MONITORING_AUTHENTICATION_AUDIENCE=api://{servicecontrol-audience}
set MONITORING_AUTHENTICATION_REQUIREHTTPSMETADATA=
set MONITORING_AUTHENTICATION_VALIDATEISSUER=
set MONITORING_AUTHENTICATION_VALIDATEAUDIENCE=

dotnet run
```

### Scenario 9: Scatter-Gather with Mismatched Authentication Configuration

Test that scatter-gather fails gracefully when remote instances have different authentication settings.

#### Query with a valid token for the primary instance

```cmd
curl --ssl-no-revoke -H "Authorization: Bearer %TOKEN%" https://localhost:33333/api/messages | json
```

You should see a warning logged in the primary isntance.

   ```text
   warn: Authentication failed when querying remote instance at https://localhost:44444. Ensure authentication is correctly configured.
   ```

---

## Group G: Mixed Configuration (Primary Only Auth)

**Restart all instances with this configuration (Scenario 12). Primary has auth, Audit and Monitoring do not.**

> [!NOTE]
> See [HTTPS Testing](https-testing.md) for certificate setup instructions using mkcert.

```cmd
rem ServiceControl (Primary) - WITH authentication
set SERVICECONTROL_HTTPS_ENABLED=true
set SERVICECONTROL_HTTPS_CERTIFICATEPATH=C:\path\to\cert\cert.pfx
set SERVICECONTROL_HTTPS_CERTIFICATEPASSWORD=changeit
set SERVICECONTROL_AUTHENTICATION_ENABLED=true
set SERVICECONTROL_AUTHENTICATION_AUTHORITY=https://login.microsoftonline.com/{tenantId}
set SERVICECONTROL_AUTHENTICATION_AUDIENCE=api://{servicecontrol-audience}
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_CLIENTID={servicepulse-clientid}
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_AUTHORITY=https://login.microsoftonline.com/{tenantId}/v2.0
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_APISCOPES=["api://{servicecontrol-audience}/access_as_user"]
set SERVICECONTROL_AUTHENTICATION_REQUIREHTTPSMETADATA=
set SERVICECONTROL_AUTHENTICATION_VALIDATEISSUER=
set SERVICECONTROL_AUTHENTICATION_VALIDATEAUDIENCE=
set SERVICECONTROL_REMOTEINSTANCES=[{"api_uri":"https://localhost:44444"}]

rem ServiceControl.Audit - WITHOUT authentication
set SERVICECONTROL_AUDIT_HTTPS_ENABLED=true
set SERVICECONTROL_AUDIT_HTTPS_CERTIFICATEPATH=C:\path\to\cert\cert.pfx
set SERVICECONTROL_AUDIT_HTTPS_CERTIFICATEPASSWORD=changeit
set SERVICECONTROL_AUDIT_AUTHENTICATION_ENABLED=
set SERVICECONTROL_AUDIT_AUTHENTICATION_AUTHORITY=
set SERVICECONTROL_AUDIT_AUTHENTICATION_AUDIENCE=
set SERVICECONTROL_AUDIT_AUTHENTICATION_REQUIREHTTPSMETADATA=
set SERVICECONTROL_AUDIT_AUTHENTICATION_VALIDATEISSUER=
set SERVICECONTROL_AUDIT_AUTHENTICATION_VALIDATEAUDIENCE=

rem ServiceControl.Monitoring - WITHOUT authentication
set MONITORING_HTTPS_ENABLED=true
set MONITORING_HTTPS_CERTIFICATEPATH=C:\path\to\cert\cert.pfx
set MONITORING_HTTPS_CERTIFICATEPASSWORD=changeit
set MONITORING_AUTHENTICATION_ENABLED=
set MONITORING_AUTHENTICATION_AUTHORITY=
set MONITORING_AUTHENTICATION_AUDIENCE=
set MONITORING_AUTHENTICATION_REQUIREHTTPSMETADATA=
set MONITORING_AUTHENTICATION_VALIDATEISSUER=
set MONITORING_AUTHENTICATION_VALIDATEAUDIENCE=

dotnet run
```

### Scenario 12: Mixed Authentication Configuration (Primary Only)

Test behavior when only the primary instance has authentication enabled, but remote instances do not.

#### Query with a valid token

```cmd
curl --ssl-no-revoke -H "Authorization: Bearer %TOKEN%" https://localhost:33333/api/messages | json
```

Logs in the primary instance show that the request was sent successfully (with auth header), and logs in the audit instance show it was received successfully.

   **Expected output:**

   ```json
   []
   ```

> [!WARNING]
> This mixed configuration is not recommended for production. If the primary requires authentication, remote instances should also require authentication to maintain consistent security.

---

## Group H: Mixed Configuration (Remotes Only Auth)

**Restart all instances with this configuration (Scenario 13). Audit and Monitoring have auth, Primary does not.**

> [!NOTE]
> See [HTTPS Testing](https-testing.md) for certificate setup instructions using mkcert.

```cmd
rem ServiceControl (Primary) - WITHOUT authentication
set SERVICECONTROL_HTTPS_ENABLED=true
set SERVICECONTROL_HTTPS_CERTIFICATEPATH=C:\path\to\cert\cert.pfx
set SERVICECONTROL_HTTPS_CERTIFICATEPASSWORD=changeit
set SERVICECONTROL_AUTHENTICATION_ENABLED=
set SERVICECONTROL_AUTHENTICATION_AUTHORITY=
set SERVICECONTROL_AUTHENTICATION_AUDIENCE=
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_CLIENTID=
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_AUTHORITY=
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_APISCOPES=
set SERVICECONTROL_AUTHENTICATION_REQUIREHTTPSMETADATA=
set SERVICECONTROL_AUTHENTICATION_VALIDATEISSUER=
set SERVICECONTROL_AUTHENTICATION_VALIDATEAUDIENCE=
set SERVICECONTROL_REMOTEINSTANCES=[{"api_uri":"https://localhost:44444"}]

rem ServiceControl.Audit - WITH authentication
set SERVICECONTROL_AUDIT_HTTPS_ENABLED=true
set SERVICECONTROL_AUDIT_HTTPS_CERTIFICATEPATH=C:\path\to\cert\cert.pfx
set SERVICECONTROL_AUDIT_HTTPS_CERTIFICATEPASSWORD=changeit
set SERVICECONTROL_AUDIT_AUTHENTICATION_ENABLED=true
set SERVICECONTROL_AUDIT_AUTHENTICATION_AUTHORITY=https://login.microsoftonline.com/{tenantId}
set SERVICECONTROL_AUDIT_AUTHENTICATION_AUDIENCE=api://{servicecontrol-audience}
set SERVICECONTROL_AUDIT_AUTHENTICATION_REQUIREHTTPSMETADATA=
set SERVICECONTROL_AUDIT_AUTHENTICATION_VALIDATEISSUER=
set SERVICECONTROL_AUDIT_AUTHENTICATION_VALIDATEAUDIENCE=

rem ServiceControl.Monitoring - WITH authentication
set MONITORING_HTTPS_ENABLED=true
set MONITORING_HTTPS_CERTIFICATEPATH=C:\path\to\cert\cert.pfx
set MONITORING_HTTPS_CERTIFICATEPASSWORD=changeit
set MONITORING_AUTHENTICATION_ENABLED=true
set MONITORING_AUTHENTICATION_AUTHORITY=https://login.microsoftonline.com/{tenantId}
set MONITORING_AUTHENTICATION_AUDIENCE=api://{servicecontrol-audience}
set MONITORING_AUTHENTICATION_REQUIREHTTPSMETADATA=
set MONITORING_AUTHENTICATION_VALIDATEISSUER=
set MONITORING_AUTHENTICATION_VALIDATEAUDIENCE=

dotnet run
```

### Scenario 13: Mixed Authentication Configuration (Remotes Only)

Test behavior when remote instances have authentication enabled, but the primary does not.

Chech the primary logs. All health checks (service-to-service) calls complete successfully as these are anonymous endpoints.

#### Query without a token

```cmd
curl --ssl-no-revoke https://localhost:33333/api/messages | json
```

The original request to the primary instance will be successfull and give the below output. If you check the primary instance logs however, there will be an error message saying the call to the audit instance failed due to authentication issues.

   **Expected output:**

   ```json
   []
   ```

   **Primary Instance Log**
   `Authentication failed when querying remote instance at https://localhost:44444. Ensure authentication is correctly configured.`

---

## See Also

- [Authentication Configuration](https://docs.particular.net/servicecontrol/security/configuration/authentication#configuration) - Configuration reference for authentication settings
- [TLS Configuration](https://docs.particular.net/servicecontrol/security/configuration/tls#configuration) - HTTPS/TLS is recommended when authentication is enabled
- [Forwarded Headers Testing](forward-headers-testing.md) - Testing forwarded headers
