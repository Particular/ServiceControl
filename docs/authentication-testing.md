# Local Testing Authentication

This guide explains how to test authentication configuration for ServiceControl instances. This approach uses curl to test authentication enforcement and configuration endpoints.

## Prerequisites

- ServiceControl built locally (see main README for build instructions)
- **HTTPS configured** - Authentication should only be used over HTTPS. Configure HTTPS using one of the methods described in [HTTPS Configuration](https-configuration.md) before testing authentication scenarios.
- **Identity Provider (IdP) configured** - For real authentication testing (Scenarios 7+), you need an OIDC provider configured with:
  - An API application registration (for ServiceControl)
  - A client application registration (for ServicePulse)
  - API scopes configured and permissions granted
  - See [Authentication Configuration](authentication.md#configuring-identity-providers) for setup instructions
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
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_AUTHORITY=
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
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_AUTHORITY=https://login.microsoftonline.com/common/v2.0
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
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_AUTHORITY=https://login.microsoftonline.com/common/v2.0
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

| Endpoint                            | Purpose                                           |
|-------------------------------------|---------------------------------------------------|
| `/api`                              | API root/discovery - returns available endpoints  |
| `/api/authentication/configuration` | Returns auth config for clients like ServicePulse |

### Scenario 5: Validation Settings Warnings

Test that disabling validation settings produces warnings in the logs.

**Start ServiceControl with relaxed validation:**

```cmd
set SERVICECONTROL_AUTHENTICATION_ENABLED=true
set SERVICECONTROL_AUTHENTICATION_AUTHORITY=https://login.microsoftonline.com/common/v2.0
set SERVICECONTROL_AUTHENTICATION_AUDIENCE=api://servicecontrol-test
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_CLIENTID=test-client-id
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_AUTHORITY=https://login.microsoftonline.com/common/v2.0
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
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_AUTHORITY=
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
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_AUTHORITY=https://login.microsoftonline.com/{tenant-id}/v2.0
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

## Multi-Instance Scenarios

The following scenarios test authentication behavior when the primary instance communicates with remote Audit and Monitoring instances.

### Scenario 8: Scatter-Gather with Authentication (Token Forwarding)

Test that the primary instance forwards authentication tokens to remote instances during scatter-gather operations.

> **Background:** When a client queries endpoints like `/api/messages`, the primary instance may query remote Audit instances to aggregate results. The client's authorization token is forwarded to these remote instances.

**Prerequisites:**

- A configured OIDC provider with valid tokens
- All instances configured with the **same** Authority and Audience settings

**Terminal 1 - Start ServiceControl.Audit with authentication:**

```cmd
set SERVICECONTROL_AUDIT_AUTHENTICATION_ENABLED=true
set SERVICECONTROL_AUDIT_AUTHENTICATION_AUTHORITY=https://login.microsoftonline.com/{tenant-id}/v2.0
set SERVICECONTROL_AUDIT_AUTHENTICATION_AUDIENCE=api://servicecontrol

cd src\ServiceControl.Audit
dotnet run
```

**Terminal 2 - Start ServiceControl (Primary) with authentication and remote instance configured:**

```cmd
set SERVICECONTROL_AUTHENTICATION_ENABLED=true
set SERVICECONTROL_AUTHENTICATION_AUTHORITY=https://login.microsoftonline.com/{tenant-id}/v2.0
set SERVICECONTROL_AUTHENTICATION_AUDIENCE=api://servicecontrol
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_CLIENTID={servicepulse-client-id}
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_AUTHORITY=https://login.microsoftonline.com/{tenant-id}/v2.0
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_APISCOPES=["api://servicecontrol/access_as_user"]
set SERVICECONTROL_REMOTEINSTANCES=[{"api_uri":"https://localhost:44444"}]

cd src\ServiceControl
dotnet run
```

**Get a test token and query the primary instance:**

```cmd
az login
set TOKEN=$(az account get-access-token --resource api://servicecontrol --query accessToken -o tsv)
curl -H "Authorization: Bearer %TOKEN%" https://localhost:33333/api/messages | json
```

**How to verify token forwarding is working:**

1. **Check the Audit instance logs (Terminal 1)** - When the request succeeds, you should see log entries showing the authenticated request was processed. Look for request logging that shows the `/api/messages` endpoint was called.

2. **Check the response headers** - The aggregated response includes instance information:

   ```cmd
   curl -v -H "Authorization: Bearer %TOKEN%" https://localhost:33333/api/messages 2>&1 | findstr /C:"X-Particular"
   ```

   You should see headers indicating responses were received from remote instances.

3. **Verify by stopping the Audit instance** - Stop the Audit instance and repeat the request. The response should now only contain local data, and the primary instance logs should show the remote is unavailable.

4. **Test direct access to Audit instance** - Verify the Audit instance requires authentication independently:

   ```cmd
   REM Without token - should fail
   curl -v https://localhost:44444/api/messages 2>&1 | findstr /C:"HTTP/"
   REM Expected: < HTTP/1.1 401 Unauthorized

   REM With token - should succeed
   curl -H "Authorization: Bearer %TOKEN%" https://localhost:44444/api/messages | json
   REM Expected: [] or list of messages
   ```

5. **Compare results** - If authentication forwarding is working correctly:
   - Direct request to Audit with token: succeeds
   - Direct request to Audit without token: fails with 401
   - Request through Primary with token: succeeds and includes Audit data
   - Request through Primary without token: fails with 401

**Test with no token (should fail):**

```cmd
curl -v https://localhost:33333/api/messages 2>&1 | findstr /C:"HTTP/"
```

**Expected output:**

```text
< HTTP/1.1 401 Unauthorized
```

### Scenario 9: Scatter-Gather with Mismatched Authentication Configuration

Test that scatter-gather fails gracefully when remote instances have different authentication settings.

**Terminal 1 - Start ServiceControl.Audit with DIFFERENT audience:**

```cmd
set SERVICECONTROL_AUDIT_AUTHENTICATION_ENABLED=true
set SERVICECONTROL_AUDIT_AUTHENTICATION_AUTHORITY=https://login.microsoftonline.com/{tenant-id}/v2.0
set SERVICECONTROL_AUDIT_AUTHENTICATION_AUDIENCE=api://servicecontrol-audit-different

cd src\ServiceControl.Audit
dotnet run
```

**Terminal 2 - Start ServiceControl (Primary):**

```cmd
set SERVICECONTROL_AUTHENTICATION_ENABLED=true
set SERVICECONTROL_AUTHENTICATION_AUTHORITY=https://login.microsoftonline.com/{tenant-id}/v2.0
set SERVICECONTROL_AUTHENTICATION_AUDIENCE=api://servicecontrol
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_CLIENTID={servicepulse-client-id}
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_AUTHORITY=https://login.microsoftonline.com/{tenant-id}/v2.0
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_APISCOPES=["api://servicecontrol/access_as_user"]
set SERVICECONTROL_REMOTEINSTANCES=[{"api_uri":"https://localhost:44444"}]

cd src\ServiceControl
dotnet run
```

**Query with a valid token for the primary instance:**

```cmd
curl -H "Authorization: Bearer %TOKEN%" https://localhost:33333/api/messages | json
```

**How to verify the mismatch is detected:**

1. **Check the Audit instance logs (Terminal 1)** - You should see a 401 Unauthorized response logged, with details about the token validation failure (audience mismatch):

   ```text
   warn: Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerHandler
         Bearer was not authenticated. Failure message: IDX10214: Audience validation failed...
   ```

2. **Check the Primary instance logs (Terminal 2)** - You should see the remote marked as temporarily unavailable:

   ```text
   warn: ... Remote instance at https://localhost:44444 returned status code Unauthorized
   ```

3. **Verify the remote status** - Check the remotes endpoint to confirm the Audit instance is marked as unavailable:

   ```cmd
   curl -H "Authorization: Bearer %TOKEN%" https://localhost:33333/api/configuration/remotes | json
   ```

   **Expected output:**

   ```json
   [
     {
       "api_uri": "https://localhost:44444",
       "status": "unavailable"
     }
   ]
   ```

4. **Confirm direct access fails with the token** - The token is valid for Primary but not for Audit:

   ```cmd
   REM Direct to Audit - should fail (wrong audience)
   curl -v -H "Authorization: Bearer %TOKEN%" https://localhost:44444/api/messages 2>&1 | findstr /C:"HTTP/"
   REM Expected: < HTTP/1.1 401 Unauthorized
   ```

### Scenario 10: Remote Instance Health Checks with Authentication

Test that the primary instance can check remote instance health when authentication is enabled.

> **Note:** The health check queries the `/api` endpoint on remote instances. This endpoint is marked as anonymous and should be accessible without authentication.

**Start both instances with authentication enabled (same configuration as Scenario 8).**

**Check the remote instances configuration endpoint:**

```cmd
curl -H "Authorization: Bearer %TOKEN%" https://localhost:33333/api/configuration/remotes | json
```

**Expected output:**

```json
[
  {
    "api_uri": "https://localhost:44444",
    "status": "online",
    "version": "5.x.x"
  }
]
```

The health check should succeed because `/api` is an anonymous endpoint.

### Scenario 11: Platform Connection Details with Authentication

Test that platform connection details can be retrieved when authentication is enabled on remote instances.

> **Note:** The primary instance queries `/api/connection` on remote instances to aggregate platform connection details. This endpoint may require authentication.

**With both instances running (same as Scenario 8):**

```cmd
curl -H "Authorization: Bearer %TOKEN%" https://localhost:33333/api/connection | json
```

**Expected behavior:**

The platform connection response includes connection details from both the primary and remote instances.

### Scenario 12: Mixed Authentication Configuration (Primary Only)

Test behavior when only the primary instance has authentication enabled, but remote instances do not.

**Terminal 1 - Start ServiceControl.Audit WITHOUT authentication:**

```cmd
set SERVICECONTROL_AUDIT_AUTHENTICATION_ENABLED=
set SERVICECONTROL_AUDIT_AUTHENTICATION_AUTHORITY=

cd src\ServiceControl.Audit
dotnet run
```

**Terminal 2 - Start ServiceControl (Primary) WITH authentication:**

```cmd
set SERVICECONTROL_AUTHENTICATION_ENABLED=true
set SERVICECONTROL_AUTHENTICATION_AUTHORITY=https://login.microsoftonline.com/{tenant-id}/v2.0
set SERVICECONTROL_AUTHENTICATION_AUDIENCE=api://servicecontrol
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_CLIENTID={servicepulse-client-id}
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_AUTHORITY=https://login.microsoftonline.com/{tenant-id}/v2.0
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_APISCOPES=["api://servicecontrol/access_as_user"]
set SERVICECONTROL_REMOTEINSTANCES=[{"api_uri":"https://localhost:44444"}]

cd src\ServiceControl
dotnet run
```

**Query with a valid token:**

```cmd
curl -H "Authorization: Bearer %TOKEN%" https://localhost:33333/api/messages | json
```

**How to verify this mixed configuration works:**

1. **Verify the Audit instance has no authentication** - Direct requests without a token should succeed:

   ```cmd
   REM Direct to Audit without token - should succeed (no auth required)
   curl https://localhost:44444/api/messages | json
   REM Expected: [] or list of messages
   ```

2. **Verify the Primary instance requires authentication** - Direct requests without a token should fail:

   ```cmd
   REM Direct to Primary without token - should fail
   curl -v https://localhost:33333/api/messages 2>&1 | findstr /C:"HTTP/"
   REM Expected: < HTTP/1.1 401 Unauthorized
   ```

3. **Check the Audit instance logs (Terminal 1)** - When queried through the Primary, you should see the request processed. The token is present in the request but ignored since authentication is disabled:

   ```text
   info: ... Processed request GET /api/messages
   ```

4. **Check the Primary instance logs (Terminal 2)** - You should see successful aggregation from the remote:

   ```text
   info: ... Successfully retrieved messages from remote https://localhost:44444
   ```

5. **Verify aggregation works** - The response from Primary should include data from both instances:

   ```cmd
   curl -H "Authorization: Bearer %TOKEN%" https://localhost:33333/api/configuration/remotes | json
   ```

   **Expected output:**

   ```json
   [
     {
       "api_uri": "https://localhost:44444",
       "status": "online",
       "version": "5.x.x"
     }
   ]
   ```

> **Security Note:** This mixed configuration is not recommended for production. If the primary requires authentication, remote instances should also require authentication to maintain consistent security.

### Scenario 13: Mixed Authentication Configuration (Remotes Only)

Test behavior when remote instances have authentication enabled, but the primary does not.

**Terminal 1 - Start ServiceControl.Audit WITH authentication:**

```cmd
set SERVICECONTROL_AUDIT_AUTHENTICATION_ENABLED=true
set SERVICECONTROL_AUDIT_AUTHENTICATION_AUTHORITY=https://login.microsoftonline.com/{tenant-id}/v2.0
set SERVICECONTROL_AUDIT_AUTHENTICATION_AUDIENCE=api://servicecontrol

cd src\ServiceControl.Audit
dotnet run
```

**Terminal 2 - Start ServiceControl (Primary) WITHOUT authentication:**

```cmd
set SERVICECONTROL_AUTHENTICATION_ENABLED=
set SERVICECONTROL_AUTHENTICATION_AUTHORITY=
set SERVICECONTROL_REMOTEINSTANCES=[{"api_uri":"https://localhost:44444"}]

cd src\ServiceControl
dotnet run
```

**Query without a token:**

```cmd
curl https://localhost:33333/api/messages | json
```

**How to verify the degraded functionality:**

1. **Verify the Primary instance has no authentication** - Direct requests without a token should succeed:

   ```cmd
   REM Direct to Primary without token - should succeed (no auth required)
   curl https://localhost:33333/api | json
   REM Expected: API root response
   ```

2. **Verify the Audit instance requires authentication** - Direct requests without a token should fail:

   ```cmd
   REM Direct to Audit without token - should fail
   curl -v https://localhost:44444/api/messages 2>&1 | findstr /C:"HTTP/"
   REM Expected: < HTTP/1.1 401 Unauthorized
   ```

3. **Check the Audit instance logs (Terminal 1)** - You should see 401 Unauthorized responses when the Primary tries to query it:

   ```text
   warn: Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerHandler
         Bearer was not authenticated. Failure message: No token provided...
   ```

4. **Check the Primary instance logs (Terminal 2)** - You should see the remote marked as temporarily unavailable:

   ```text
   warn: ... Remote instance at https://localhost:44444 returned status code Unauthorized
   ```

5. **Verify the remote is marked unavailable:**

   ```cmd
   curl https://localhost:33333/api/configuration/remotes | json
   ```

   **Expected output:**

   ```json
   [
     {
       "api_uri": "https://localhost:44444",
       "status": "unavailable"
     }
   ]
   ```

6. **Confirm scatter-gather returns partial results** - The response only contains local Primary data, not aggregated Audit data. Any endpoints or messages stored in the Audit instance will be missing from the response.

> **Warning:** This configuration results in degraded functionality. Remote instances will be inaccessible for scatter-gather operations.

### Scenario 14: Expired Token Forwarding

Test how scatter-gather handles expired tokens being forwarded to remote instances.

**With both instances running with authentication (same as Scenario 8):**

**Use an expired token:**

```cmd
curl -v -H "Authorization: Bearer {expired-token}" https://localhost:33333/api/messages 2>&1 | findstr /C:"HTTP/"
```

**Expected output:**

```text
< HTTP/1.1 401 Unauthorized
```

The primary instance rejects the expired token before any remote requests are made.

## Known Limitations

### Internal Service-to-Service Communication

The following internal API calls from the primary instance to remote instances do **not** forward authentication headers:

| Internal Call       | Endpoint                                                      | Purpose                                |
|---------------------|---------------------------------------------------------------|----------------------------------------|
| Health Check        | `GET /api`                                                    | Verify remote instance availability    |
| Configuration       | `GET /api/configuration`                                      | Retrieve remote instance configuration |
| Platform Connection | `GET /api/connection`                                         | Aggregate platform connection details  |
| License Throughput  | `GET /api/endpoints`, `GET /api/endpoints/{name}/audit-count` | Collect audit throughput for licensing |

**Implications:**

- These endpoints must be accessible without authentication for multi-instance deployments to work
- The `/api` endpoint is already marked as anonymous on all instances
- The `/api/configuration` endpoint on Audit and Monitoring instances should allow anonymous access for inter-instance communication

### Same Authentication Configuration Required

When using scatter-gather with authentication enabled:

- All instances (Primary, Audit, Monitoring) must use the **same** Authority and Audience
- Client tokens must be valid for all instances
- There is no service-to-service authentication mechanism; client tokens are forwarded directly

### Token Forwarding Security Considerations

- Client tokens are forwarded to remote instances in their entirety
- Remote instances see the same token as the primary instance
- Token scope/claims are not modified during forwarding

## Testing Other Instances

The scenarios above use ServiceControl (Primary). To test ServiceControl.Audit or ServiceControl.Monitoring:

1. Use the appropriate environment variable prefix (see Instance Reference above)
2. Use the corresponding project directory and port
3. Note: Audit and Monitoring instances don't require ServicePulse settings

| Instance                  | Project Directory               | Port  | Env Var Prefix          |
|---------------------------|---------------------------------|-------|-------------------------|
| ServiceControl (Primary)  | `src\ServiceControl`            | 33333 | `SERVICECONTROL_`       |
| ServiceControl.Audit      | `src\ServiceControl.Audit`      | 44444 | `SERVICECONTROL_AUDIT_` |
| ServiceControl.Monitoring | `src\ServiceControl.Monitoring` | 33633 | `MONITORING_`           |

## Cleanup

After testing, clear the environment variables:

**Command Prompt (cmd):**

```cmd
set SERVICECONTROL_AUTHENTICATION_ENABLED=
set SERVICECONTROL_AUTHENTICATION_AUTHORITY=
set SERVICECONTROL_AUTHENTICATION_AUDIENCE=
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_CLIENTID=
set SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_AUTHORITY=
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
$env:SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_AUTHORITY = $null
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
- [Forwarded Headers Testing](forward-headers-testing.md) - Testing forwarded headers
