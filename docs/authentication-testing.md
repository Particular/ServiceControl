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
set SERVICECONTROL_HTTPS_CERTIFICATEPATH=C:\Users\warwi\source\ServiceControl\.local\certs\localhost.pfx
set SERVICECONTROL_HTTPS_CERTIFICATEPASSWORD=changeit
set SERVICECONTROL_AUTHENTICATION_ENABLED=true
set SERVICECONTROL_AUTHENTICATION_AUTHORITY=https://login.microsoftonline.com/7d9a1798-bf74-4c3a-9892-5ca91ab54c1b
set SERVICECONTROL_AUTHENTICATION_AUDIENCE=api://670a9843-4d0e-40ab-9ffc-299e6f2ddb02
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_CLIENTID=c99d0ff5-bb0a-4131-a079-a4ac542b9615
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_AUTHORITY=https://login.microsoftonline.com/7d9a1798-bf74-4c3a-9892-5ca91ab54c1b/v2.0
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_APISCOPES=["api://670a9843-4d0e-40ab-9ffc-299e6f2ddb02/access_as_user"]
set SERVICECONTROL_AUTHENTICATION_REQUIREHTTPSMETADATA=
set SERVICECONTROL_AUTHENTICATION_VALIDATEISSUER=
set SERVICECONTROL_AUTHENTICATION_VALIDATEAUDIENCE=
set SERVICECONTROL_REMOTEINSTANCES=[{"api_uri":"https://localhost:44444"}]

rem ServiceControl.Audit
set SERVICECONTROL_AUDIT_HTTPS_ENABLED=true
set SERVICECONTROL_AUDIT_HTTPS_CERTIFICATEPATH=C:\Users\warwi\source\ServiceControl\.local\certs\localhost.pfx
set SERVICECONTROL_AUDIT_HTTPS_CERTIFICATEPASSWORD=changeit
set SERVICECONTROL_AUDIT_AUTHENTICATION_ENABLED=true
set SERVICECONTROL_AUDIT_AUTHENTICATION_AUTHORITY=https://login.microsoftonline.com/7d9a1798-bf74-4c3a-9892-5ca91ab54c1b
set SERVICECONTROL_AUDIT_AUTHENTICATION_AUDIENCE=api://670a9843-4d0e-40ab-9ffc-299e6f2ddb02
set SERVICECONTROL_AUDIT_AUTHENTICATION_REQUIREHTTPSMETADATA=
set SERVICECONTROL_AUDIT_AUTHENTICATION_VALIDATEISSUER=
set SERVICECONTROL_AUDIT_AUTHENTICATION_VALIDATEAUDIENCE=

rem ServiceControl.Monitoring
set MONITORING_HTTPS_ENABLED=true
set MONITORING_HTTPS_CERTIFICATEPATH=C:\Users\warwi\source\ServiceControl\.local\certs\localhost.pfx
set MONITORING_HTTPS_CERTIFICATEPASSWORD=changeit
set MONITORING_AUTHENTICATION_ENABLED=true
set MONITORING_AUTHENTICATION_AUTHORITY=https://login.microsoftonline.com/7d9a1798-bf74-4c3a-9892-5ca91ab54c1b
set MONITORING_AUTHENTICATION_AUDIENCE=api://670a9843-4d0e-40ab-9ffc-299e6f2ddb02
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
set SERVICECONTROL_HTTPS_CERTIFICATEPATH=C:\Users\warwi\source\ServiceControl\.local\certs\localhost.pfx
set SERVICECONTROL_HTTPS_CERTIFICATEPASSWORD=changeit
set SERVICECONTROL_AUTHENTICATION_ENABLED=true
set SERVICECONTROL_AUTHENTICATION_AUTHORITY=https://login.microsoftonline.com/7d9a1798-bf74-4c3a-9892-5ca91ab54c1b
set SERVICECONTROL_AUTHENTICATION_AUDIENCE=api://670a9843-4d0e-40ab-9ffc-299e6f2ddb02
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_CLIENTID=c99d0ff5-bb0a-4131-a079-a4ac542b9615
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_AUTHORITY=https://login.microsoftonline.com/7d9a1798-bf74-4c3a-9892-5ca91ab54c1b/v2.0
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_APISCOPES=["api://670a9843-4d0e-40ab-9ffc-299e6f2ddb02/access_as_user"]
set SERVICECONTROL_AUTHENTICATION_REQUIREHTTPSMETADATA=
set SERVICECONTROL_AUTHENTICATION_VALIDATEISSUER=
set SERVICECONTROL_AUTHENTICATION_VALIDATEAUDIENCE=
set SERVICECONTROL_REMOTEINSTANCES=[{"api_uri":"https://localhost:44444"}]

rem ServiceControl.Audit (DIFFERENT audience)
set SERVICECONTROL_AUDIT_HTTPS_ENABLED=true
set SERVICECONTROL_AUDIT_HTTPS_CERTIFICATEPATH=C:\Users\warwi\source\ServiceControl\.local\certs\localhost.pfx
set SERVICECONTROL_AUDIT_HTTPS_CERTIFICATEPASSWORD=changeit
set SERVICECONTROL_AUDIT_AUTHENTICATION_ENABLED=true
set SERVICECONTROL_AUDIT_AUTHENTICATION_AUTHORITY=https://login.microsoftonline.com/7d9a1798-bf74-4c3a-9892-5ca91ab54c1b
set SERVICECONTROL_AUDIT_AUTHENTICATION_AUDIENCE=api://servicecontrol-audit-different
set SERVICECONTROL_AUDIT_AUTHENTICATION_REQUIREHTTPSMETADATA=
set SERVICECONTROL_AUDIT_AUTHENTICATION_VALIDATEISSUER=
set SERVICECONTROL_AUDIT_AUTHENTICATION_VALIDATEAUDIENCE=

rem ServiceControl.Monitoring
set MONITORING_HTTPS_ENABLED=true
set MONITORING_HTTPS_CERTIFICATEPATH=C:\Users\warwi\source\ServiceControl\.local\certs\localhost.pfx
set MONITORING_HTTPS_CERTIFICATEPASSWORD=changeit
set MONITORING_AUTHENTICATION_ENABLED=true
set MONITORING_AUTHENTICATION_AUTHORITY=https://login.microsoftonline.com/7d9a1798-bf74-4c3a-9892-5ca91ab54c1b
set MONITORING_AUTHENTICATION_AUDIENCE=api://670a9843-4d0e-40ab-9ffc-299e6f2ddb02
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
set SERVICECONTROL_HTTPS_CERTIFICATEPATH=C:\Users\warwi\source\ServiceControl\.local\certs\localhost.pfx
set SERVICECONTROL_HTTPS_CERTIFICATEPASSWORD=changeit
set SERVICECONTROL_AUTHENTICATION_ENABLED=true
set SERVICECONTROL_AUTHENTICATION_AUTHORITY=https://login.microsoftonline.com/7d9a1798-bf74-4c3a-9892-5ca91ab54c1b
set SERVICECONTROL_AUTHENTICATION_AUDIENCE=api://670a9843-4d0e-40ab-9ffc-299e6f2ddb02
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_CLIENTID=c99d0ff5-bb0a-4131-a079-a4ac542b9615
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_AUTHORITY=https://login.microsoftonline.com/7d9a1798-bf74-4c3a-9892-5ca91ab54c1b/v2.0
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_APISCOPES=["api://670a9843-4d0e-40ab-9ffc-299e6f2ddb02/access_as_user"]
set SERVICECONTROL_AUTHENTICATION_REQUIREHTTPSMETADATA=
set SERVICECONTROL_AUTHENTICATION_VALIDATEISSUER=
set SERVICECONTROL_AUTHENTICATION_VALIDATEAUDIENCE=
set SERVICECONTROL_REMOTEINSTANCES=[{"api_uri":"https://localhost:44444"}]

rem ServiceControl.Audit - WITHOUT authentication
set SERVICECONTROL_AUDIT_HTTPS_ENABLED=true
set SERVICECONTROL_AUDIT_HTTPS_CERTIFICATEPATH=C:\Users\warwi\source\ServiceControl\.local\certs\localhost.pfx
set SERVICECONTROL_AUDIT_HTTPS_CERTIFICATEPASSWORD=changeit
set SERVICECONTROL_AUDIT_AUTHENTICATION_ENABLED=
set SERVICECONTROL_AUDIT_AUTHENTICATION_AUTHORITY=
set SERVICECONTROL_AUDIT_AUTHENTICATION_AUDIENCE=
set SERVICECONTROL_AUDIT_AUTHENTICATION_REQUIREHTTPSMETADATA=
set SERVICECONTROL_AUDIT_AUTHENTICATION_VALIDATEISSUER=
set SERVICECONTROL_AUDIT_AUTHENTICATION_VALIDATEAUDIENCE=

rem ServiceControl.Monitoring - WITHOUT authentication
set MONITORING_HTTPS_ENABLED=true
set MONITORING_HTTPS_CERTIFICATEPATH=C:\Users\warwi\source\ServiceControl\.local\certs\localhost.pfx
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
set SERVICECONTROL_HTTPS_CERTIFICATEPATH=C:\Users\warwi\source\ServiceControl\.local\certs\localhost.pfx
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
set SERVICECONTROL_AUDIT_HTTPS_CERTIFICATEPATH=C:\Users\warwi\source\ServiceControl\.local\certs\localhost.pfx
set SERVICECONTROL_AUDIT_HTTPS_CERTIFICATEPASSWORD=changeit
set SERVICECONTROL_AUDIT_AUTHENTICATION_ENABLED=true
set SERVICECONTROL_AUDIT_AUTHENTICATION_AUTHORITY=https://login.microsoftonline.com/7d9a1798-bf74-4c3a-9892-5ca91ab54c1b
set SERVICECONTROL_AUDIT_AUTHENTICATION_AUDIENCE=api://670a9843-4d0e-40ab-9ffc-299e6f2ddb02
set SERVICECONTROL_AUDIT_AUTHENTICATION_REQUIREHTTPSMETADATA=
set SERVICECONTROL_AUDIT_AUTHENTICATION_VALIDATEISSUER=
set SERVICECONTROL_AUDIT_AUTHENTICATION_VALIDATEAUDIENCE=

rem ServiceControl.Monitoring - WITH authentication
set MONITORING_HTTPS_ENABLED=true
set MONITORING_HTTPS_CERTIFICATEPATH=C:\Users\warwi\source\ServiceControl\.local\certs\localhost.pfx
set MONITORING_HTTPS_CERTIFICATEPASSWORD=changeit
set MONITORING_AUTHENTICATION_ENABLED=true
set MONITORING_AUTHENTICATION_AUTHORITY=https://login.microsoftonline.com/7d9a1798-bf74-4c3a-9892-5ca91ab54c1b
set MONITORING_AUTHENTICATION_AUDIENCE=api://670a9843-4d0e-40ab-9ffc-299e6f2ddb02
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

## Scenario 15: Complete API Endpoint Authentication Testing

This scenario tests every API endpoint with and without authentication enabled to verify authentication is correctly enforced across all routes.

### Test Procedure

For each endpoint:

1. **Auth Disabled**: Configure Group A, verify endpoint returns expected response
2. **Auth Enabled (No Token)**: Configure Group E, verify protected endpoints return `401 Unauthorized`
3. **Auth Enabled (Valid Token)**: Configure Group E with valid token, verify endpoint returns expected response

### Scatter-Gather Behavior Reference

Some endpoints on the Primary instance aggregate data from remote Audit or Monitoring instances. This is called "scatter-gather". When testing these endpoints, verify both the primary response AND the expected remote instance behavior.

**Authentication Forwarding Rules:**

- **Scatter-Gather endpoints**: Auth token IS forwarded to remote instances
- **Health checks**: Auth token is NOT forwarded (remote `/api` endpoint is anonymous)
- **Remote configuration queries**: Auth token is NOT forwarded (remote `/api/configuration` is anonymous)

| Primary Endpoint                                          | Remote Instance   | Remote Endpoint Called                                    | Auth Forwarded | Notes                                                  |
|-----------------------------------------------------------|-------------------|-----------------------------------------------------------|----------------|--------------------------------------------------------|
| `GET /api/messages`                                       | Audit             | `GET /api/messages`                                       | Yes            | Aggregates messages from all Audit instances           |
| `GET /api/messages2`                                      | Audit             | `GET /api/messages` or `/api/messages/search`             | Yes            | Enhanced endpoint with time range filtering            |
| `GET /api/messages/search`                                | Audit             | `GET /api/messages/search`                                | Yes            | Full text search across all Audit instances            |
| `GET /api/messages/search/{keyword}`                      | Audit             | `GET /api/messages/search/{keyword}`                      | Yes            | Keyword search across all Audit instances              |
| `GET /api/messages/{id}/body`                             | Audit             | `GET /api/messages/{id}/body`                             | Yes            | Forwards to specific instance via `instance_id` param  |
| `GET /api/endpoints/{endpoint}/messages`                  | Audit             | `GET /api/endpoints/{endpoint}/messages`                  | Yes            | Messages for endpoint from all Audit instances         |
| `GET /api/endpoints/{endpoint}/messages/search`           | Audit             | `GET /api/endpoints/{endpoint}/messages/search`           | Yes            | Search for endpoint across Audit instances             |
| `GET /api/endpoints/{endpoint}/messages/search/{keyword}` | Audit             | `GET /api/endpoints/{endpoint}/messages/search/{keyword}` | Yes            | Keyword search for endpoint                            |
| `GET /api/endpoints/{endpoint}/audit-count`               | Audit             | `GET /api/endpoints/{endpoint}/audit-count`               | Yes            | Counts aggregated across Audit instances               |
| `GET /api/conversations/{conversationId}`                 | Audit             | `GET /api/conversations/{conversationId}`                 | Yes            | Conversation from all Audit instances                  |
| `GET /api/sagas/{id}`                                     | Audit             | `GET /api/sagas/{id}`                                     | Yes            | Saga data exists only on Audit instances               |
| `GET /api/endpoints/known`                                | Audit             | `GET /api/endpoints/known`                                | Yes            | Known endpoints from all Audit instances               |
| `GET /api/connection`                                     | Audit             | `GET /api/connection`                                     | Yes            | Aggregates connection details from remotes             |
| `GET /api/configuration/remotes`                          | Audit, Monitoring | `GET /api/configuration`                                  | No             | Remote config is anonymous endpoint                    |
| `POST /api/errors/{failedMessageId}/retry`                | Primary (other)   | `POST /api/errors/{failedMessageId}/retry`                | Yes            | Forwards if `instance_id` points to different instance |
| Health Check (internal)                                   | Audit, Monitoring | `GET /api` or `GET /`                                     | No             | Anonymous endpoint, runs every 30 seconds              |

**Expected Scatter-Gather Behavior with Authentication:**

| Scenario                     | Primary Response    | Audit Instance Logs                          | Notes                                         |
|------------------------------|---------------------|----------------------------------------------|-----------------------------------------------|
| Auth disabled on both        | 200 OK with data    | Request received, no auth header             | Normal operation                              |
| Auth enabled, valid token    | 200 OK with data    | Request received with auth header, validated | Token forwarded and accepted                  |
| Auth enabled, no token       | 401 Unauthorized    | No request received                          | Rejected at Primary                           |
| Auth enabled on Primary only | 200 OK with data    | Request received with auth header (ignored)  | Token forwarded, Audit doesn't validate       |
| Auth enabled on Audit only   | 200 OK (empty data) | Request received, 401 returned               | Primary logs warning, returns partial results |
| Mismatched audiences         | 200 OK (empty data) | Request received, token rejected (401)       | Primary logs warning about auth failure       |

### ServiceControl (Primary Instance) Endpoints

| Method  | Route                                                   | Description                              | Requires Auth  | Expected (Auth Enabled, No Token) | Expected (Auth Enabled, Valid Token) |
|---------|---------------------------------------------------------|------------------------------------------|----------------|-----------------------------------|--------------------------------------|
| GET     | `/api`                                                  | Root URLs and instance description       | No (Anonymous) | 200 OK with JSON                  | 200 OK with JSON                     |
| GET     | `/api/instance-info`                                    | Configuration information                | No (Anonymous) | 200 OK with config                | 200 OK with config                   |
| GET     | `/api/configuration`                                    | Configuration information (alias)        | No (Anonymous) | 200 OK with config                | 200 OK with config                   |
| GET     | `/api/authentication/configuration`                     | Authentication configuration for clients | No (Anonymous) | 200 OK with auth config           | 200 OK with auth config              |
| GET     | `/api/configuration/remotes`                            | Remote instance configurations           | Yes            | 401 Unauthorized                  | 200 OK with remotes array            |
| GET     | `/api/errors`                                           | Query failed messages                    | Yes            | 401 Unauthorized                  | 200 OK with errors array             |
| HEAD    | `/api/errors`                                           | Get error count headers                  | Yes            | 401 Unauthorized                  | 200 OK with headers                  |
| GET     | `/api/errors/summary`                                   | Error statistics summary                 | Yes            | 401 Unauthorized                  | 200 OK with summary                  |
| GET     | `/api/errors/{failedMessageId}`                         | Get error details by ID                  | Yes            | 401 Unauthorized                  | 200 OK / 404 Not Found               |
| GET     | `/api/errors/last/{failedMessageId}`                    | Get last retry attempt                   | Yes            | 401 Unauthorized                  | 200 OK / 404 Not Found               |
| POST    | `/api/errors/archive`                                   | Archive batch of errors                  | Yes            | 401 Unauthorized                  | 200 OK                               |
| PATCH   | `/api/errors/archive`                                   | Archive batch of errors (alias)          | Yes            | 401 Unauthorized                  | 200 OK                               |
| POST    | `/api/errors/{messageId}/archive`                       | Archive single error                     | Yes            | 401 Unauthorized                  | 200 OK / 404 Not Found               |
| PATCH   | `/api/errors/{messageId}/archive`                       | Archive single error (alias)             | Yes            | 401 Unauthorized                  | 200 OK / 404 Not Found               |
| PATCH   | `/api/errors/unarchive`                                 | Unarchive batch of messages              | Yes            | 401 Unauthorized                  | 200 OK                               |
| PATCH   | `/api/errors/{from}...{to}/unarchive`                   | Unarchive by date range                  | Yes            | 401 Unauthorized                  | 200 OK                               |
| POST    | `/api/errors/{failedMessageId}/retry`                   | Retry single failed message              | Yes            | 401 Unauthorized                  | 202 Accepted / 404 Not Found         |
| POST    | `/api/errors/retry`                                     | Retry multiple messages by IDs           | Yes            | 401 Unauthorized                  | 202 Accepted                         |
| POST    | `/api/errors/queues/{queueAddress}/retry`               | Retry all errors from queue              | Yes            | 401 Unauthorized                  | 202 Accepted                         |
| POST    | `/api/errors/retry/all`                                 | Retry all failed messages                | Yes            | 401 Unauthorized                  | 202 Accepted                         |
| POST    | `/api/errors/{endpointName}/retry/all`                  | Retry all errors from endpoint           | Yes            | 401 Unauthorized                  | 202 Accepted                         |
| GET     | `/api/errors/groups/{classifier}`                       | Get failure groups by classifier         | Yes            | 401 Unauthorized                  | 200 OK with groups                   |
| GET     | `/api/errors/queues/addresses`                          | Get all queue addresses                  | Yes            | 401 Unauthorized                  | 200 OK with addresses                |
| GET     | `/api/errors/queues/addresses/search/{search}`          | Search queue addresses                   | Yes            | 401 Unauthorized                  | 200 OK with addresses                |
| GET     | `/api/endpoints/{endpointName}/errors`                  | Get errors for endpoint                  | Yes            | 401 Unauthorized                  | 200 OK with errors                   |
| PATCH   | `/api/pendingretries/resolve`                           | Resolve pending retry messages           | Yes            | 401 Unauthorized                  | 200 OK                               |
| PATCH   | `/api/pendingretries/queues/resolve`                    | Resolve pending retries for queue        | Yes            | 401 Unauthorized                  | 200 OK                               |
| POST    | `/api/pendingretries/retry`                             | Retry pending retries by IDs             | Yes            | 401 Unauthorized                  | 202 Accepted                         |
| POST    | `/api/pendingretries/queues/retry`                      | Retry pending retries for queue          | Yes            | 401 Unauthorized                  | 202 Accepted                         |
| GET     | `/api/archive/groups/id/{groupId}`                      | Get archive group details                | Yes            | 401 Unauthorized                  | 200 OK / 404 Not Found               |
| GET     | `/api/edit/config`                                      | Get edit configuration                   | Yes            | 401 Unauthorized                  | 200 OK with config                   |
| POST    | `/api/edit/{failedMessageId}`                           | Edit and retry failed message            | Yes            | 401 Unauthorized                  | 200 OK / 404 Not Found               |
| GET     | `/api/recoverability/classifiers`                       | Get failure classifiers                  | Yes            | 401 Unauthorized                  | 200 OK with classifiers              |
| GET     | `/api/recoverability/groups/{classifier}`               | Get failure groups                       | Yes            | 401 Unauthorized                  | 200 OK with groups                   |
| GET     | `/api/recoverability/groups/{groupId}/errors`           | Get errors in group                      | Yes            | 401 Unauthorized                  | 200 OK with errors                   |
| HEAD    | `/api/recoverability/groups/{groupId}/errors`           | Get error count for group                | Yes            | 401 Unauthorized                  | 200 OK with headers                  |
| GET     | `/api/recoverability/groups/id/{groupId}`               | Get group details                        | Yes            | 401 Unauthorized                  | 200 OK / 404 Not Found               |
| GET     | `/api/recoverability/history`                           | Get retry history                        | Yes            | 401 Unauthorized                  | 200 OK with history                  |
| POST    | `/api/recoverability/groups/{groupId}/comment`          | Add comment to group                     | Yes            | 401 Unauthorized                  | 200 OK                               |
| DELETE  | `/api/recoverability/groups/{groupId}/comment`          | Delete group comment                     | Yes            | 401 Unauthorized                  | 200 OK                               |
| POST    | `/api/recoverability/groups/{groupId}/errors/archive`   | Archive all errors in group              | Yes            | 401 Unauthorized                  | 202 Accepted                         |
| POST    | `/api/recoverability/groups/{groupId}/errors/unarchive` | Unarchive all errors in group            | Yes            | 401 Unauthorized                  | 202 Accepted                         |
| POST    | `/api/recoverability/groups/{groupId}/errors/retry`     | Retry all errors in group                | Yes            | 401 Unauthorized                  | 202 Accepted                         |
| DELETE  | `/api/recoverability/unacknowledgedgroups/{groupId}`    | Acknowledge retry operation              | Yes            | 401 Unauthorized                  | 200 OK                               |
| GET     | `/api/messages`                                         | Get all messages (scatter-gather)        | Yes            | 401 Unauthorized                  | 200 OK with messages                 |
| GET     | `/api/messages2`                                        | Get messages with date filtering         | Yes            | 401 Unauthorized                  | 200 OK with messages                 |
| GET     | `/api/messages/{id}/body`                               | Get message body                         | Yes            | 401 Unauthorized                  | 200 OK with body                     |
| GET     | `/api/messages/search`                                  | Full text search messages                | Yes            | 401 Unauthorized                  | 200 OK with results                  |
| GET     | `/api/messages/search/{keyword}`                        | Search by keyword                        | Yes            | 401 Unauthorized                  | 200 OK with results                  |
| GET     | `/api/endpoints/{endpoint}/messages`                    | Get messages for endpoint                | Yes            | 401 Unauthorized                  | 200 OK with messages                 |
| GET     | `/api/endpoints/{endpoint}/messages/search`             | Search messages for endpoint             | Yes            | 401 Unauthorized                  | 200 OK with results                  |
| GET     | `/api/endpoints/{endpoint}/messages/search/{keyword}`   | Search by keyword for endpoint           | Yes            | 401 Unauthorized                  | 200 OK with results                  |
| GET     | `/api/endpoints/{endpoint}/audit-count`                 | Get audit counts for endpoint            | Yes            | 401 Unauthorized                  | 200 OK with counts                   |
| GET     | `/api/conversations/{conversationId}`                   | Get messages in conversation             | Yes            | 401 Unauthorized                  | 200 OK with messages                 |
| GET     | `/api/customchecks`                                     | Get all custom checks                    | Yes            | 401 Unauthorized                  | 200 OK with checks                   |
| DELETE  | `/api/customchecks/{id}`                                | Delete custom check                      | Yes            | 401 Unauthorized                  | 200 OK / 404 Not Found               |
| GET     | `/api/connection`                                       | Get platform connection details          | Yes            | 401 Unauthorized                  | 200 OK with connection info          |
| GET     | `/api/eventlogitems`                                    | Get event log items                      | Yes            | 401 Unauthorized                  | 200 OK with events                   |
| GET     | `/api/license`                                          | Get license information                  | Yes            | 401 Unauthorized                  | 200 OK with license                  |
| POST    | `/api/redirects`                                        | Create message redirect                  | Yes            | 401 Unauthorized                  | 201 Created / 409 Conflict           |
| PUT     | `/api/redirects/{messageRedirectId}`                    | Update redirect destination              | Yes            | 401 Unauthorized                  | 200 OK / 404 Not Found               |
| DELETE  | `/api/redirects/{messageRedirectId}`                    | Delete message redirect                  | Yes            | 401 Unauthorized                  | 200 OK / 404 Not Found               |
| HEAD    | `/api/redirect`                                         | Get redirect count                       | Yes            | 401 Unauthorized                  | 200 OK with headers                  |
| GET     | `/api/redirects`                                        | List all redirects                       | Yes            | 401 Unauthorized                  | 200 OK with redirects                |
| GET     | `/api/heartbeats/stats`                                 | Get heartbeat statistics                 | Yes            | 401 Unauthorized                  | 200 OK with stats                    |
| GET     | `/api/endpoints`                                        | Get monitored endpoints                  | Yes            | 401 Unauthorized                  | 200 OK with endpoints                |
| OPTIONS | `/api/endpoints`                                        | Get allowed HTTP methods                 | Yes            | 401 Unauthorized                  | 200 OK with Allow header             |
| DELETE  | `/api/endpoints/{endpointId}`                           | Delete/unmonitor endpoint                | Yes            | 401 Unauthorized                  | 200 OK / 404 Not Found               |
| PATCH   | `/api/endpoints/{endpointId}`                           | Enable/disable monitoring                | Yes            | 401 Unauthorized                  | 200 OK / 404 Not Found               |
| GET     | `/api/endpoints/known`                                  | Get known endpoints                      | Yes            | 401 Unauthorized                  | 200 OK with endpoints                |
| GET     | `/api/endpointssettings`                                | Get endpoint settings                    | Yes            | 401 Unauthorized                  | 200 OK with settings                 |
| PATCH   | `/api/endpointssettings/{endpointName}`                 | Update endpoint settings                 | Yes            | 401 Unauthorized                  | 200 OK / 404 Not Found               |
| GET     | `/api/notifications/email`                              | Get email notification settings          | Yes            | 401 Unauthorized                  | 200 OK with settings                 |
| POST    | `/api/notifications/email`                              | Update email settings                    | Yes            | 401 Unauthorized                  | 200 OK                               |
| POST    | `/api/notifications/email/toggle`                       | Toggle email notifications               | Yes            | 401 Unauthorized                  | 200 OK                               |
| POST    | `/api/notifications/email/test`                         | Send test email                          | Yes            | 401 Unauthorized                  | 200 OK / error                       |
| GET     | `/api/sagas/{id}`                                       | Get saga history by ID                   | Yes            | 401 Unauthorized                  | 200 OK with saga / 404               |

### ServiceControl.Audit Endpoints

| Method | Route                                                 | Description                        | Requires Auth  | Expected (Auth Enabled, No Token) | Expected (Auth Enabled, Valid Token) |
|--------|-------------------------------------------------------|------------------------------------|----------------|-----------------------------------|--------------------------------------|
| GET    | `/api`                                                | Root URLs and instance description | No (Anonymous) | 200 OK with JSON                  | 200 OK with JSON                     |
| GET    | `/api/instance-info`                                  | Configuration information          | No (Anonymous) | 200 OK with config                | 200 OK with config                   |
| GET    | `/api/configuration`                                  | Configuration information (alias)  | No (Anonymous) | 200 OK with config                | 200 OK with config                   |
| GET    | `/api/messages`                                       | Get all audit messages             | Yes            | 401 Unauthorized                  | 200 OK with messages                 |
| GET    | `/api/messages2`                                      | Get messages with date filtering   | Yes            | 401 Unauthorized                  | 200 OK with messages                 |
| GET    | `/api/messages/{id}/body`                             | Get message body                   | Yes            | 401 Unauthorized                  | 200 OK with body                     |
| GET    | `/api/messages/search`                                | Full text search messages          | Yes            | 401 Unauthorized                  | 200 OK with results                  |
| GET    | `/api/messages/search/{keyword}`                      | Search by keyword                  | Yes            | 401 Unauthorized                  | 200 OK with results                  |
| GET    | `/api/endpoints/{endpoint}/messages`                  | Get messages for endpoint          | Yes            | 401 Unauthorized                  | 200 OK with messages                 |
| GET    | `/api/endpoints/{endpoint}/messages/search`           | Search messages for endpoint       | Yes            | 401 Unauthorized                  | 200 OK with results                  |
| GET    | `/api/endpoints/{endpoint}/messages/search/{keyword}` | Search by keyword for endpoint     | Yes            | 401 Unauthorized                  | 200 OK with results                  |
| GET    | `/api/endpoints/{endpoint}/audit-count`               | Get audit counts for endpoint      | Yes            | 401 Unauthorized                  | 200 OK with counts                   |
| GET    | `/api/conversations/{conversationId}`                 | Get messages in conversation       | Yes            | 401 Unauthorized                  | 200 OK with messages                 |
| GET    | `/api/endpoints/known`                                | Get known endpoints                | Yes            | 401 Unauthorized                  | 200 OK with endpoints                |
| GET    | `/api/sagas/{id}`                                     | Get saga history by ID             | Yes            | 401 Unauthorized                  | 200 OK with saga / 404               |
| GET    | `/api/connection`                                     | Get audit connection details       | Yes            | 401 Unauthorized                  | 200 OK with connection               |

### ServiceControl.Monitoring Endpoints

| Method  | Route                                   | Description                           | Requires Auth  | Expected (Auth Enabled, No Token) | Expected (Auth Enabled, Valid Token) |
|---------|-----------------------------------------|---------------------------------------|----------------|-----------------------------------|--------------------------------------|
| GET     | `/`                                     | Root metadata (instanceType, version) | No (Anonymous) | 200 OK with JSON                  | 200 OK with JSON                     |
| OPTIONS | `/`                                     | Get allowed HTTP methods              | No (Anonymous) | 200 OK with Allow header          | 200 OK with Allow header             |
| GET     | `/connection`                           | Get monitoring connection details     | Yes            | 401 Unauthorized                  | 200 OK with connection               |
| GET     | `/license`                              | Get license information               | Yes            | 401 Unauthorized                  | 200 OK with license                  |
| GET     | `/monitored-endpoints`                  | Get monitored endpoint metrics        | Yes            | 401 Unauthorized                  | 200 OK with metrics                  |
| GET     | `/monitored-endpoints/disconnected`     | Get disconnected endpoints            | Yes            | 401 Unauthorized                  | 200 OK with endpoints                |

### Automated Testing with PowerShell Script

A PowerShell script is available at `src/Scripts/Test-AuthenticationEndpoints.ps1` to automate endpoint testing.

#### Getting a Token

```powershell
# Get a token first (if testing with authentication enabled)
# Using Azure CLI:
az login
$Token = (az account get-access-token --resource "api://{audience}" --query accessToken -o tsv)
```

#### Group A: Authentication Disabled

**Instance Configuration:**

No authentication environment variables needed. Clear any previously set auth variables and start instances:

```cmd
rem ServiceControl (Primary) - Terminal 1
set SERVICECONTROL_AUTHENTICATION_ENABLED=
cd src\ServiceControl
dotnet run

rem ServiceControl.Audit - Terminal 2
set SERVICECONTROL_AUDIT_AUTHENTICATION_ENABLED=
cd src\ServiceControl.Audit
dotnet run

rem ServiceControl.Monitoring - Terminal 3
set MONITORING_AUTHENTICATION_ENABLED=
cd src\ServiceControl.Monitoring
dotnet run
```

**Run Tests:**

```powershell
# Test all instances with auth disabled (HTTP)
.\src\Scripts\Test-AuthenticationEndpoints.ps1 -Mode AuthDisabled `
    -PrimaryUrl "http://localhost:33333" `
    -AuditUrl "http://localhost:44444" `
    -MonitoringUrl "http://localhost:33633"
```

#### Group B/E: Authentication Enabled - No Token (Expect 401)

**Instance Configuration:**

Set authentication environment variables for all instances. Replace placeholders with your real IdP values:

```cmd
rem ServiceControl (Primary)
set SERVICECONTROL_HTTPS_ENABLED=true
set SERVICECONTROL_HTTPS_CERTIFICATEPATH=C:\Users\warwi\source\ServiceControl\.local\certs\localhost.pfx
set SERVICECONTROL_HTTPS_CERTIFICATEPASSWORD=changeit
set SERVICECONTROL_AUTHENTICATION_ENABLED=true
set SERVICECONTROL_AUTHENTICATION_AUTHORITY=https://login.microsoftonline.com/7d9a1798-bf74-4c3a-9892-5ca91ab54c1b
set SERVICECONTROL_AUTHENTICATION_AUDIENCE=api://670a9843-4d0e-40ab-9ffc-299e6f2ddb02
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_CLIENTID=c99d0ff5-bb0a-4131-a079-a4ac542b9615
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_AUTHORITY=https://login.microsoftonline.com/7d9a1798-bf74-4c3a-9892-5ca91ab54c1b/v2.0
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_APISCOPES=["api://670a9843-4d0e-40ab-9ffc-299e6f2ddb02/access_as_user"]
set SERVICECONTROL_AUTHENTICATION_REQUIREHTTPSMETADATA=
set SERVICECONTROL_AUTHENTICATION_VALIDATEISSUER=
set SERVICECONTROL_AUTHENTICATION_VALIDATEAUDIENCE=
set SERVICECONTROL_REMOTEINSTANCES=[{"api_uri":"https://localhost:44444"}]

rem ServiceControl.Audit
set SERVICECONTROL_AUDIT_HTTPS_ENABLED=true
set SERVICECONTROL_AUDIT_HTTPS_CERTIFICATEPATH=C:\Users\warwi\source\ServiceControl\.local\certs\localhost.pfx
set SERVICECONTROL_AUDIT_HTTPS_CERTIFICATEPASSWORD=changeit
set SERVICECONTROL_AUDIT_AUTHENTICATION_ENABLED=true
set SERVICECONTROL_AUDIT_AUTHENTICATION_AUTHORITY=https://login.microsoftonline.com/7d9a1798-bf74-4c3a-9892-5ca91ab54c1b
set SERVICECONTROL_AUDIT_AUTHENTICATION_AUDIENCE=api://670a9843-4d0e-40ab-9ffc-299e6f2ddb02
set SERVICECONTROL_AUDIT_AUTHENTICATION_REQUIREHTTPSMETADATA=
set SERVICECONTROL_AUDIT_AUTHENTICATION_VALIDATEISSUER=
set SERVICECONTROL_AUDIT_AUTHENTICATION_VALIDATEAUDIENCE=

rem ServiceControl.Monitoring
set MONITORING_HTTPS_ENABLED=true
set MONITORING_HTTPS_CERTIFICATEPATH=C:\Users\warwi\source\ServiceControl\.local\certs\localhost.pfx
set MONITORING_HTTPS_CERTIFICATEPASSWORD=changeit
set MONITORING_AUTHENTICATION_ENABLED=true
set MONITORING_AUTHENTICATION_AUTHORITY=https://login.microsoftonline.com/7d9a1798-bf74-4c3a-9892-5ca91ab54c1b
set MONITORING_AUTHENTICATION_AUDIENCE=api://670a9843-4d0e-40ab-9ffc-299e6f2ddb02
set MONITORING_AUTHENTICATION_REQUIREHTTPSMETADATA=
set MONITORING_AUTHENTICATION_VALIDATEISSUER=
set MONITORING_AUTHENTICATION_VALIDATEAUDIENCE=

dotnet run
```

**Run Tests:**

```powershell
# Test all instances - protected endpoints should return 401
.\src\Scripts\Test-AuthenticationEndpoints.ps1 -Mode AuthEnabledNoToken `
  -SkipCertificateCheck
```

#### Group E: Authentication Enabled - With Valid Token

Uses the same instance configuration as Group B/E above.

**Run Tests:**

```powershell
# Test all instances with a valid token
.\src\Scripts\Test-AuthenticationEndpoints.ps1 -Mode AuthEnabledWithToken `
    -Token $Token `
    -SkipCertificateCheck
```

---

## See Also

- [Authentication Configuration](https://docs.particular.net/servicecontrol/security/configuration/authentication#configuration) - Configuration reference for authentication settings
- [TLS Configuration](https://docs.particular.net/servicecontrol/security/configuration/tls#configuration) - HTTPS/TLS is recommended when authentication is enabled
- [Forwarded Headers Testing](forward-headers-testing.md) - Testing forwarded headers
