<#
.SYNOPSIS
    Tests all ServiceControl API endpoints for authentication behavior.

.DESCRIPTION
    This script tests every API endpoint across Primary, Audit, and Monitoring instances
    to verify authentication is working correctly. It supports three test modes:
    - AuthDisabled: Tests that all endpoints work without authentication
    - AuthEnabledNoToken: Tests that protected endpoints return 401 without a token
    - AuthEnabledWithToken: Tests that all endpoints work with a valid token

.PARAMETER Mode
    The test mode to run:
    - AuthDisabled: All endpoints should return success (no auth required)
    - AuthEnabledNoToken: Protected endpoints should return 401, anonymous should return 200
    - AuthEnabledWithToken: All endpoints should return success with valid token

.PARAMETER PrimaryUrl
    Base URL for the Primary ServiceControl instance. Default: https://localhost:33333

.PARAMETER AuditUrl
    Base URL for the Audit ServiceControl instance. Default: https://localhost:44444

.PARAMETER MonitoringUrl
    Base URL for the Monitoring ServiceControl instance. Default: https://localhost:33633

.PARAMETER Token
    Bearer token for authenticated requests. Required for AuthEnabledWithToken mode.

.PARAMETER SkipCertificateCheck
    Skip SSL certificate validation (useful for self-signed certs in testing).

.PARAMETER TestPrimary
    Test only the Primary instance endpoints.

.PARAMETER TestAudit
    Test only the Audit instance endpoints.

.PARAMETER TestMonitoring
    Test only the Monitoring instance endpoints.

.EXAMPLE
    # Test with authentication disabled (Group A configuration)
    .\Test-AuthenticationEndpoints.ps1 -Mode AuthDisabled

.EXAMPLE
    # Test with authentication enabled, no token (should get 401 on protected endpoints)
    .\Test-AuthenticationEndpoints.ps1 -Mode AuthEnabledNoToken -SkipCertificateCheck

.EXAMPLE
    # Test with a valid token
    .\Test-AuthenticationEndpoints.ps1 -Mode AuthEnabledWithToken -Token "eyJ0eXAi..." -SkipCertificateCheck

.EXAMPLE
    # Test only Primary instance
    .\Test-AuthenticationEndpoints.ps1 -Mode AuthDisabled -TestPrimary
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("AuthDisabled", "AuthEnabledNoToken", "AuthEnabledWithToken")]
    [string]$Mode,

    [string]$PrimaryUrl = "https://localhost:33333",
    [string]$AuditUrl = "https://localhost:44444",
    [string]$MonitoringUrl = "https://localhost:33633",

    [string]$Token,

    [switch]$SkipCertificateCheck,
    [switch]$TestPrimary,
    [switch]$TestAudit,
    [switch]$TestMonitoring
)

# If no specific instance selected, test all
if (-not $TestPrimary -and -not $TestAudit -and -not $TestMonitoring) {
    $TestPrimary = $true
    $TestAudit = $true
    $TestMonitoring = $true
}

#region Endpoint Definitions

# Primary instance endpoints
$PrimaryEndpoints = @(
    # Anonymous endpoints
    @{ Method = "GET"; Path = "/api"; Anonymous = $true; Description = "Root URLs and instance description" }
    @{ Method = "GET"; Path = "/api/instance-info"; Anonymous = $true; Description = "Configuration information" }
    @{ Method = "GET"; Path = "/api/configuration"; Anonymous = $true; Description = "Configuration information (alias)" }
    @{ Method = "GET"; Path = "/api/authentication/configuration"; Anonymous = $true; Description = "Authentication configuration" }

    # Protected endpoints - Configuration
    @{ Method = "GET"; Path = "/api/configuration/remotes"; Anonymous = $false; Description = "Remote instance configurations" }

    # Protected endpoints - Errors
    @{ Method = "GET"; Path = "/api/errors"; Anonymous = $false; Description = "Query failed messages" }
    @{ Method = "HEAD"; Path = "/api/errors"; Anonymous = $false; Description = "Get error count headers" }
    @{ Method = "GET"; Path = "/api/errors/summary"; Anonymous = $false; Description = "Error statistics summary" }
    @{ Method = "GET"; Path = "/api/errors/test-message-id"; Anonymous = $false; Description = "Get error details by ID" }
    @{ Method = "GET"; Path = "/api/errors/last/test-message-id"; Anonymous = $false; Description = "Get last retry attempt" }
    @{ Method = "POST"; Path = "/api/errors/archive"; Anonymous = $false; Description = "Archive batch of errors" }
    @{ Method = "PATCH"; Path = "/api/errors/archive"; Anonymous = $false; Description = "Archive batch of errors (alias)" }
    @{ Method = "POST"; Path = "/api/errors/test-message-id/archive"; Anonymous = $false; Description = "Archive single error" }
    @{ Method = "PATCH"; Path = "/api/errors/test-message-id/archive"; Anonymous = $false; Description = "Archive single error (alias)" }
    @{ Method = "PATCH"; Path = "/api/errors/unarchive"; Anonymous = $false; Description = "Unarchive batch of messages" }
    @{ Method = "POST"; Path = "/api/errors/test-message-id/retry"; Anonymous = $false; Description = "Retry single failed message" }
    @{ Method = "POST"; Path = "/api/errors/retry"; Anonymous = $false; Description = "Retry multiple messages by IDs" }
    @{ Method = "POST"; Path = "/api/errors/queues/test-queue/retry"; Anonymous = $false; Description = "Retry all errors from queue" }
    @{ Method = "POST"; Path = "/api/errors/retry/all"; Anonymous = $false; Description = "Retry all failed messages" }
    @{ Method = "POST"; Path = "/api/errors/test-endpoint/retry/all"; Anonymous = $false; Description = "Retry all errors from endpoint" }
    @{ Method = "GET"; Path = "/api/errors/groups/Exception%20Type%20and%20Stack%20Trace"; Anonymous = $false; Description = "Get failure groups by classifier" }
    @{ Method = "GET"; Path = "/api/errors/queues/addresses"; Anonymous = $false; Description = "Get all queue addresses" }
    @{ Method = "GET"; Path = "/api/errors/queues/addresses/search/test"; Anonymous = $false; Description = "Search queue addresses" }
    @{ Method = "GET"; Path = "/api/endpoints/test-endpoint/errors"; Anonymous = $false; Description = "Get errors for endpoint" }

    # Protected endpoints - Pending Retries
    @{ Method = "PATCH"; Path = "/api/pendingretries/resolve"; Anonymous = $false; Description = "Resolve pending retry messages" }
    @{ Method = "PATCH"; Path = "/api/pendingretries/queues/resolve"; Anonymous = $false; Description = "Resolve pending retries for queue" }
    @{ Method = "POST"; Path = "/api/pendingretries/retry"; Anonymous = $false; Description = "Retry pending retries by IDs" }
    @{ Method = "POST"; Path = "/api/pendingretries/queues/retry"; Anonymous = $false; Description = "Retry pending retries for queue" }

    # Protected endpoints - Archive Groups
    @{ Method = "GET"; Path = "/api/archive/groups/id/test-group-id"; Anonymous = $false; Description = "Get archive group details" }

    # Protected endpoints - Edit
    @{ Method = "GET"; Path = "/api/edit/config"; Anonymous = $false; Description = "Get edit configuration" }
    @{ Method = "POST"; Path = "/api/edit/test-message-id"; Anonymous = $false; Description = "Edit and retry failed message" }

    # Protected endpoints - Recoverability
    @{ Method = "GET"; Path = "/api/recoverability/classifiers"; Anonymous = $false; Description = "Get failure classifiers" }
    @{ Method = "GET"; Path = "/api/recoverability/groups/Exception%20Type%20and%20Stack%20Trace"; Anonymous = $false; Description = "Get failure groups" }
    @{ Method = "GET"; Path = "/api/recoverability/groups/test-group-id/errors"; Anonymous = $false; Description = "Get errors in group" }
    @{ Method = "HEAD"; Path = "/api/recoverability/groups/test-group-id/errors"; Anonymous = $false; Description = "Get error count for group" }
    @{ Method = "GET"; Path = "/api/recoverability/groups/id/test-group-id"; Anonymous = $false; Description = "Get group details" }
    @{ Method = "GET"; Path = "/api/recoverability/history"; Anonymous = $false; Description = "Get retry history" }
    @{ Method = "POST"; Path = "/api/recoverability/groups/test-group-id/comment"; Anonymous = $false; Description = "Add comment to group" }
    @{ Method = "DELETE"; Path = "/api/recoverability/groups/test-group-id/comment"; Anonymous = $false; Description = "Delete group comment" }
    @{ Method = "POST"; Path = "/api/recoverability/groups/test-group-id/errors/archive"; Anonymous = $false; Description = "Archive all errors in group" }
    @{ Method = "POST"; Path = "/api/recoverability/groups/test-group-id/errors/unarchive"; Anonymous = $false; Description = "Unarchive all errors in group" }
    @{ Method = "POST"; Path = "/api/recoverability/groups/test-group-id/errors/retry"; Anonymous = $false; Description = "Retry all errors in group" }
    @{ Method = "DELETE"; Path = "/api/recoverability/unacknowledgedgroups/test-group-id"; Anonymous = $false; Description = "Acknowledge retry operation" }

    # Protected endpoints - Messages (scatter-gather)
    @{ Method = "GET"; Path = "/api/messages"; Anonymous = $false; Description = "Get all messages (scatter-gather)" }
    @{ Method = "GET"; Path = "/api/messages2"; Anonymous = $false; Description = "Get messages with date filtering" }
    @{ Method = "GET"; Path = "/api/messages/test-message-id/body"; Anonymous = $false; Description = "Get message body" }
    @{ Method = "GET"; Path = "/api/messages/search?q=test"; Anonymous = $false; Description = "Full text search messages" }
    @{ Method = "GET"; Path = "/api/messages/search/test"; Anonymous = $false; Description = "Search by keyword" }
    @{ Method = "GET"; Path = "/api/endpoints/test-endpoint/messages"; Anonymous = $false; Description = "Get messages for endpoint" }
    @{ Method = "GET"; Path = "/api/endpoints/test-endpoint/messages/search"; Anonymous = $false; Description = "Search messages for endpoint" }
    @{ Method = "GET"; Path = "/api/endpoints/test-endpoint/messages/search/test"; Anonymous = $false; Description = "Search by keyword for endpoint" }
    @{ Method = "GET"; Path = "/api/endpoints/test-endpoint/audit-count"; Anonymous = $false; Description = "Get audit counts for endpoint" }

    # Protected endpoints - Conversations
    @{ Method = "GET"; Path = "/api/conversations/test-conversation-id"; Anonymous = $false; Description = "Get messages in conversation" }

    # Protected endpoints - Custom Checks
    @{ Method = "GET"; Path = "/api/customchecks"; Anonymous = $false; Description = "Get all custom checks" }
    @{ Method = "DELETE"; Path = "/api/customchecks/test-check-id"; Anonymous = $false; Description = "Delete custom check" }

    # Protected endpoints - Connection
    @{ Method = "GET"; Path = "/api/connection"; Anonymous = $false; Description = "Get platform connection details" }

    # Protected endpoints - Event Log
    @{ Method = "GET"; Path = "/api/eventlogitems"; Anonymous = $false; Description = "Get event log items" }

    # Protected endpoints - License
    @{ Method = "GET"; Path = "/api/license"; Anonymous = $false; Description = "Get license information" }

    # Protected endpoints - Redirects
    @{ Method = "POST"; Path = "/api/redirects"; Anonymous = $false; Description = "Create message redirect" }
    @{ Method = "PUT"; Path = "/api/redirects/test-redirect-id"; Anonymous = $false; Description = "Update redirect destination" }
    @{ Method = "DELETE"; Path = "/api/redirects/test-redirect-id"; Anonymous = $false; Description = "Delete message redirect" }
    @{ Method = "HEAD"; Path = "/api/redirect"; Anonymous = $false; Description = "Get redirect count" }
    @{ Method = "GET"; Path = "/api/redirects"; Anonymous = $false; Description = "List all redirects" }

    # Protected endpoints - Heartbeats
    @{ Method = "GET"; Path = "/api/heartbeats/stats"; Anonymous = $false; Description = "Get heartbeat statistics" }

    # Protected endpoints - Endpoints
    @{ Method = "GET"; Path = "/api/endpoints"; Anonymous = $false; Description = "Get monitored endpoints" }
    @{ Method = "OPTIONS"; Path = "/api/endpoints"; Anonymous = $true; Description = "Get allowed HTTP methods" }
    @{ Method = "DELETE"; Path = "/api/endpoints/test-endpoint-id"; Anonymous = $false; Description = "Delete/unmonitor endpoint" }
    @{ Method = "PATCH"; Path = "/api/endpoints/test-endpoint-id"; Anonymous = $false; Description = "Enable/disable monitoring" }
    @{ Method = "GET"; Path = "/api/endpoints/known"; Anonymous = $false; Description = "Get known endpoints" }

    # Protected endpoints - Endpoint Settings
    @{ Method = "GET"; Path = "/api/endpointssettings"; Anonymous = $false; Description = "Get endpoint settings" }
    @{ Method = "PATCH"; Path = "/api/endpointssettings/test-endpoint"; Anonymous = $false; Description = "Update endpoint settings" }

    # Protected endpoints - Notifications
    @{ Method = "GET"; Path = "/api/notifications/email"; Anonymous = $false; Description = "Get email notification settings" }
    @{ Method = "POST"; Path = "/api/notifications/email"; Anonymous = $false; Description = "Update email settings" }
    @{ Method = "POST"; Path = "/api/notifications/email/toggle"; Anonymous = $false; Description = "Toggle email notifications" }
    @{ Method = "POST"; Path = "/api/notifications/email/test"; Anonymous = $false; Description = "Send test email" }

    # Protected endpoints - Sagas
    @{ Method = "GET"; Path = "/api/sagas/test-saga-id"; Anonymous = $false; Description = "Get saga history by ID" }
)

# Audit instance endpoints
$AuditEndpoints = @(
    # Anonymous endpoints
    @{ Method = "GET"; Path = "/api"; Anonymous = $true; Description = "Root URLs and instance description" }
    @{ Method = "GET"; Path = "/api/instance-info"; Anonymous = $true; Description = "Configuration information" }
    @{ Method = "GET"; Path = "/api/configuration"; Anonymous = $true; Description = "Configuration information (alias)" }

    # Protected endpoints - Messages
    @{ Method = "GET"; Path = "/api/messages"; Anonymous = $false; Description = "Get all audit messages" }
    @{ Method = "GET"; Path = "/api/messages2"; Anonymous = $false; Description = "Get messages with date filtering" }
    @{ Method = "GET"; Path = "/api/messages/test-message-id/body"; Anonymous = $false; Description = "Get message body" }
    @{ Method = "GET"; Path = "/api/messages/search"; Anonymous = $false; Description = "Full text search messages" }
    @{ Method = "GET"; Path = "/api/messages/search/test"; Anonymous = $false; Description = "Search by keyword" }
    @{ Method = "GET"; Path = "/api/endpoints/test-endpoint/messages"; Anonymous = $false; Description = "Get messages for endpoint" }
    @{ Method = "GET"; Path = "/api/endpoints/test-endpoint/messages/search"; Anonymous = $false; Description = "Search messages for endpoint" }
    @{ Method = "GET"; Path = "/api/endpoints/test-endpoint/messages/search/test"; Anonymous = $false; Description = "Search by keyword for endpoint" }
    @{ Method = "GET"; Path = "/api/endpoints/test-endpoint/audit-count"; Anonymous = $false; Description = "Get audit counts for endpoint" }

    # Protected endpoints - Conversations
    @{ Method = "GET"; Path = "/api/conversations/test-conversation-id"; Anonymous = $false; Description = "Get messages in conversation" }

    # Protected endpoints - Known Endpoints
    @{ Method = "GET"; Path = "/api/endpoints/known"; Anonymous = $false; Description = "Get known endpoints" }

    # Protected endpoints - Sagas
    @{ Method = "GET"; Path = "/api/sagas/test-saga-id"; Anonymous = $false; Description = "Get saga history by ID" }

    # Protected endpoints - Connection
    @{ Method = "GET"; Path = "/api/connection"; Anonymous = $false; Description = "Get audit connection details" }
)

# Monitoring instance endpoints
$MonitoringEndpoints = @(
    # Anonymous endpoints
    @{ Method = "GET"; Path = "/"; Anonymous = $true; Description = "Root metadata (instanceType, version)" }
    @{ Method = "OPTIONS"; Path = "/"; Anonymous = $true; Description = "Get allowed HTTP methods" }

    # Protected endpoints
    @{ Method = "GET"; Path = "/connection"; Anonymous = $false; Description = "Get monitoring connection details" }
    @{ Method = "GET"; Path = "/license"; Anonymous = $false; Description = "Get license information" }
    @{ Method = "GET"; Path = "/monitored-endpoints"; Anonymous = $false; Description = "Get monitored endpoint metrics" }
    @{ Method = "GET"; Path = "/monitored-endpoints/disconnected"; Anonymous = $false; Description = "Get disconnected endpoints" }
)

#endregion

#region Helper Functions

# Check if we're running PowerShell Core 6+ (which supports -SkipCertificateCheck)
$script:IsPowerShellCore = $PSVersionTable.PSVersion.Major -ge 6

# For Windows PowerShell 5.1, we need to use a different approach for skipping cert validation
if (-not $script:IsPowerShellCore -and $SkipCertificateCheck) {
    # Disable certificate validation for the session
    Add-Type @"
using System.Net;
using System.Security.Cryptography.X509Certificates;
public class TrustAllCertsPolicy : ICertificatePolicy {
    public bool CheckValidationResult(
        ServicePoint srvPoint, X509Certificate certificate,
        WebRequest request, int certificateProblem) {
        return true;
    }
}
"@
    [System.Net.ServicePointManager]::CertificatePolicy = New-Object TrustAllCertsPolicy
    [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12
}

function Test-Endpoint {
    param(
        [string]$BaseUrl,
        [hashtable]$Endpoint,
        [string]$Token,
        [string]$Mode,
        [switch]$SkipCertificateCheck
    )

    $url = "$BaseUrl$($Endpoint.Path)"
    $method = $Endpoint.Method
    $isAnonymous = $Endpoint.Anonymous
    $description = $Endpoint.Description

    # Build request parameters
    $params = @{
        Uri = $url
        Method = $method
        UseBasicParsing = $true
        ErrorAction = "Stop"
    }

    # Only add SkipCertificateCheck parameter for PowerShell Core 6+
    if ($SkipCertificateCheck -and $script:IsPowerShellCore) {
        $params.SkipCertificateCheck = $true
    }

    # Add token if provided and mode requires it
    if ($Token -and $Mode -eq "AuthEnabledWithToken") {
        $params.Headers = @{
            "Authorization" = "Bearer $Token"
        }
    }

    # Determine expected status code
    $expectedSuccess = switch ($Mode) {
        "AuthDisabled" { $true }
        "AuthEnabledNoToken" { $isAnonymous }
        "AuthEnabledWithToken" { $true }
    }

    try {
        $response = Invoke-WebRequest @params
        $statusCode = $response.StatusCode
        $success = $statusCode -ge 200 -and $statusCode -lt 400

        if ($expectedSuccess -and $success) {
            return @{
                Url = $url
                Method = $method
                Description = $description
                StatusCode = $statusCode
                Expected = "Success"
                Actual = "Success"
                Passed = $true
            }
        }
        elseif (-not $expectedSuccess -and $success) {
            return @{
                Url = $url
                Method = $method
                Description = $description
                StatusCode = $statusCode
                Expected = "401 Unauthorized"
                Actual = "Success ($statusCode)"
                Passed = $false
            }
        }
    }
    catch {
        $statusCode = 0
        if ($_.Exception.Response) {
            $statusCode = [int]$_.Exception.Response.StatusCode
        }
        # Fallback: try to extract status code from error message (e.g., "(400) Bad Request")
        if ($statusCode -eq 0 -and $_.Exception.Message -match '\((\d{3})\)') {
            $statusCode = [int]$Matches[1]
        }

        if (-not $expectedSuccess -and $statusCode -eq 401) {
            # Expected 401 and got 401 - auth is working correctly
            return @{
                Url = $url
                Method = $method
                Description = $description
                StatusCode = $statusCode
                Expected = "401 Unauthorized"
                Actual = "401 Unauthorized"
                Passed = $true
            }
        }
        elseif ($expectedSuccess -and $statusCode -eq 404) {
            # Expected success and got 404 - auth passed but resource not found
            # For auth testing purposes, this counts as "auth passed"
            return @{
                Url = $url
                Method = $method
                Description = $description
                StatusCode = $statusCode
                Expected = "Success"
                Actual = "Auth OK (Not Found)"
                Passed = $true
            }
        }
        elseif (-not $expectedSuccess -and $statusCode -eq 404) {
            # Expected 401 but got 404 - means request got through auth when it shouldn't have
            return @{
                Url = $url
                Method = $method
                Description = $description
                StatusCode = $statusCode
                Expected = "401 Unauthorized"
                Actual = "Auth Bypassed (Not Found)"
                Passed = $false
            }
        }
        elseif ($expectedSuccess -and $statusCode -ge 400 -and $statusCode -lt 500 -and $statusCode -ne 401 -and $statusCode -ne 403) {
            # Expected success and got 4xx (not 401/403) - auth passed but request was invalid
            # Common cases: 400 Bad Request, 415 Unsupported Media Type, 409 Conflict
            # For auth testing purposes, this counts as "auth passed"
            return @{
                Url = $url
                Method = $method
                Description = $description
                StatusCode = $statusCode
                Expected = "Success"
                Actual = "Auth OK (Client Error $statusCode)"
                Passed = $true
            }
        }
        elseif (-not $expectedSuccess -and $statusCode -ge 400 -and $statusCode -lt 500 -and $statusCode -ne 401 -and $statusCode -ne 403) {
            # Expected 401 but got other 4xx - means request got through auth when it shouldn't have
            return @{
                Url = $url
                Method = $method
                Description = $description
                StatusCode = $statusCode
                Expected = "401 Unauthorized"
                Actual = "Auth Bypassed (Client Error $statusCode)"
                Passed = $false
            }
        }
        elseif ($expectedSuccess -and $statusCode -ge 500) {
            # Expected success and got 5xx - auth passed but endpoint had server error
            # For auth testing purposes, this counts as "auth passed"
            return @{
                Url = $url
                Method = $method
                Description = $description
                StatusCode = $statusCode
                Expected = "Success"
                Actual = "Auth OK (Server Error $statusCode)"
                Passed = $true
            }
        }
        elseif (-not $expectedSuccess -and $statusCode -ge 500) {
            # Expected 401 but got 5xx - means request got through auth when it shouldn't have
            return @{
                Url = $url
                Method = $method
                Description = $description
                StatusCode = $statusCode
                Expected = "401 Unauthorized"
                Actual = "Auth Bypassed (Server Error $statusCode)"
                Passed = $false
            }
        }
        else {
            return @{
                Url = $url
                Method = $method
                Description = $description
                StatusCode = $statusCode
                Expected = if ($expectedSuccess) { "Success" } else { "401 Unauthorized" }
                Actual = "Error: $($_.Exception.Message)"
                Passed = $false
            }
        }
    }
}

function Write-TestResult {
    param([hashtable]$Result)

    $icon = if ($Result.Passed) { "[PASS]" } else { "[FAIL]" }
    $color = if ($Result.Passed) { "Green" } else { "Red" }

    Write-Host "$icon " -ForegroundColor $color -NoNewline
    Write-Host "$($Result.Method.PadRight(7)) $($Result.Url)" -NoNewline

    if (-not $Result.Passed) {
        Write-Host " (Expected: $($Result.Expected), Got: $($Result.Actual))" -ForegroundColor Yellow
    }
    else {
        Write-Host " ($($Result.StatusCode))" -ForegroundColor DarkGray
    }
}

function Test-InstanceEndpoints {
    param(
        [string]$InstanceName,
        [string]$BaseUrl,
        [array]$Endpoints,
        [string]$Token,
        [string]$Mode,
        [switch]$SkipCertificateCheck
    )

    Write-Host ""
    Write-Host "Testing $InstanceName Instance ($BaseUrl)" -ForegroundColor Cyan
    Write-Host ("=" * 60) -ForegroundColor Cyan

    $results = @()
    $passed = 0
    $failed = 0

    foreach ($endpoint in $Endpoints) {
        $result = Test-Endpoint -BaseUrl $BaseUrl -Endpoint $endpoint -Token $Token -Mode $Mode -SkipCertificateCheck:$SkipCertificateCheck
        $results += $result
        Write-TestResult $result

        if ($result.Passed) { $passed++ } else { $failed++ }
    }

    Write-Host ""
    Write-Host "Results: " -NoNewline
    Write-Host "$passed passed" -ForegroundColor Green -NoNewline
    Write-Host ", " -NoNewline
    if ($failed -gt 0) {
        Write-Host "$failed failed" -ForegroundColor Red
    }
    else {
        Write-Host "$failed failed" -ForegroundColor Green
    }

    return @{
        InstanceName = $InstanceName
        Results = $results
        Passed = $passed
        Failed = $failed
    }
}

#endregion

#region Main Script

Write-Host ""
Write-Host "ServiceControl Authentication Endpoint Tests" -ForegroundColor Magenta
Write-Host "=============================================" -ForegroundColor Magenta
Write-Host ""
Write-Host "Mode: $Mode" -ForegroundColor White

# Validate token is provided when needed
if ($Mode -eq "AuthEnabledWithToken" -and -not $Token) {
    Write-Host "Error: AuthEnabledWithToken mode requires a token." -ForegroundColor Red
    Write-Host "Provide -Token parameter with a valid bearer token." -ForegroundColor Yellow
    exit 1
}

$allResults = @()

# Test instances
if ($TestPrimary) {
    $primaryResults = Test-InstanceEndpoints -InstanceName "Primary" -BaseUrl $PrimaryUrl -Endpoints $PrimaryEndpoints -Token $Token -Mode $Mode -SkipCertificateCheck:$SkipCertificateCheck
    $allResults += $primaryResults
}

if ($TestAudit) {
    $auditResults = Test-InstanceEndpoints -InstanceName "Audit" -BaseUrl $AuditUrl -Endpoints $AuditEndpoints -Token $Token -Mode $Mode -SkipCertificateCheck:$SkipCertificateCheck
    $allResults += $auditResults
}

if ($TestMonitoring) {
    $monitoringResults = Test-InstanceEndpoints -InstanceName "Monitoring" -BaseUrl $MonitoringUrl -Endpoints $MonitoringEndpoints -Token $Token -Mode $Mode -SkipCertificateCheck:$SkipCertificateCheck
    $allResults += $monitoringResults
}

# Summary
Write-Host ""
Write-Host "=============================================" -ForegroundColor Magenta
Write-Host "SUMMARY" -ForegroundColor Magenta
Write-Host "=============================================" -ForegroundColor Magenta

$totalPassed = 0
$totalFailed = 0
foreach ($result in $allResults) {
    $totalPassed += $result.Passed
    $totalFailed += $result.Failed
}
$totalTests = $totalPassed + $totalFailed

Write-Host ""
Write-Host "Total Tests: $totalTests" -ForegroundColor White
Write-Host "Passed:      $totalPassed" -ForegroundColor Green
if ($totalFailed -gt 0) {
    Write-Host "Failed:      $totalFailed" -ForegroundColor Red
}
else {
    Write-Host "Failed:      $totalFailed" -ForegroundColor Green
}

Write-Host ""

if ($totalFailed -gt 0) {
    Write-Host "FAILED TESTS:" -ForegroundColor Red
    foreach ($instanceResult in $allResults) {
        foreach ($result in $instanceResult.Results) {
            if (-not $result.Passed) {
                Write-Host "  - [$($instanceResult.InstanceName)] $($result.Method) $($result.Url)" -ForegroundColor Red
                Write-Host "    Expected: $($result.Expected), Got: $($result.Actual)" -ForegroundColor Yellow
            }
        }
    }
    exit 1
}
else {
    Write-Host "All tests passed!" -ForegroundColor Green
    exit 0
}

#endregion
