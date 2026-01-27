# Local Testing Forwarded Headers (Without NGINX)

This guide explains how to test forwarded headers configuration for ServiceControl instances without using NGINX or Docker. This approach uses curl to manually send `X-Forwarded-*` headers directly to the instances.

## Prerequisites

- ServiceControl built locally (see [main README for instructions](../README.md#how-to-rundebug-locally))
- curl (included with Windows 10/11, Git Bash, or WSL)
- (Optional) For formatted JSON output: `npm install -g json` then pipe curl output through `| json`
- All commands assume you are in the respective project directory

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

Debug logs will show detailed forwarded headers processing and trust evaluation information.

## Instance Reference

| Instance                  | Project Directory               | Default Port | Environment Variable Prefix |
|---------------------------|---------------------------------|--------------|-----------------------------|
| ServiceControl (Primary)  | `src\ServiceControl`            | 33333        | `SERVICECONTROL_`           |
| ServiceControl.Audit      | `src\ServiceControl.Audit`      | 44444        | `SERVICECONTROL_AUDIT_`     |
| ServiceControl.Monitoring | `src\ServiceControl.Monitoring` | 33633        | `MONITORING_`               |

> [!NOTE]
> Environment variables must include the instance prefix (e.g., `SERVICECONTROL_FORWARDEDHEADERS_ENABLED` for the primary instance).

## How Forwarded Headers Work

When a ServiceControl instance is behind a reverse proxy, the proxy sends headers to indicate the original request details:

- `X-Forwarded-For` - Original client IP address
- `X-Forwarded-Proto` - Original protocol (http/https)
- `X-Forwarded-Host` - Original host header

Each instance can be configured to trust these headers from specific proxies or trust all proxies.

### Trust Evaluation Rules

The middleware determines whether to process forwarded headers based on these rules:

1. **If `TrustAllProxies` = true**: All requests are trusted, headers are always processed
2. **If `TrustAllProxies` = false**: The caller's IP must match **either**:
   - **KnownProxies**: Exact IP address match (e.g., `127.0.0.1`, `::1`)
   - **KnownNetworks**: CIDR range match (e.g., `127.0.0.0/8`, `10.0.0.0/8`)

> [!IMPORTANT]
> KnownProxies and KnownNetworks use **OR logic** - a match in either grants trust. The check is against the **immediate caller's IP** (the proxy connecting to ServiceControl), not the original client IP from `X-Forwarded-For`.

## Configuration Methods

Settings can be configured via:

1. **Environment variables** (recommended for testing) - Easy to change between scenarios, no file edits needed
2. **App.config** - Persisted settings, requires app restart after changes

Both methods work identically. This guide uses environment variables for convenience during iterative testing.

## Test Scenarios

> [!IMPORTANT]
> Set environment variables in the same terminal where you run `dotnet run`. Environment variables are scoped to the terminal session and won't be seen if you run from Visual Studio or a different terminal.
> Check the application startup logs to verify which settings were applied. The forwarded headers configuration is logged at startup.

### Test Grouping by Configuration

To minimize service restarts during testing, scenarios are grouped by configuration. Run all tests within a group before changing configuration:

| Configuration Group                    | Scenarios          | Description                                                  |
|----------------------------------------|--------------------|--------------------------------------------------------------|
| **Group A**: Default/TrustAllProxies   | 0, 1, 2, 8, 11, 13 | Tests with default settings or explicit TrustAllProxies=true |
| **Group B**: KnownProxies (localhost)  | 3, 9, 14           | Tests with KnownProxies=127.0.0.1,::1                        |
| **Group C**: KnownNetworks (localhost) | 4                  | Tests with KnownNetworks=127.0.0.0/8,::1/128                 |
| **Group D**: Untrusted Proxy           | 5                  | Tests with KnownProxies=192.168.1.100                        |
| **Group E**: Untrusted Network         | 6                  | Tests with KnownNetworks=10.0.0.0/8,192.168.0.0/16           |
| **Group F**: Disabled                  | 7                  | Tests with Enabled=false                                     |
| **Group G**: Combined                  | 10                 | Tests with both KnownProxies and KnownNetworks               |
| **Group H**: IPv4 Only                 | 12                 | Tests with KnownProxies=127.0.0.1 (no IPv6)                  |

---

## Group A: Default/TrustAllProxies Configuration

**Start the instance once, then run all tests in this group (Scenarios 0, 1, 2, 8, 11, 13).**

```cmd
rem Cleanup and start - ServiceControl (Primary)
set SERVICECONTROL_FORWARDEDHEADERS_ENABLED=
set SERVICECONTROL_FORWARDEDHEADERS_TRUSTALLPROXIES=
set SERVICECONTROL_FORWARDEDHEADERS_KNOWNPROXIES=
set SERVICECONTROL_FORWARDEDHEADERS_KNOWNNETWORKS=

rem ServiceControl.Audit
set SERVICECONTROL_AUDIT_FORWARDEDHEADERS_ENABLED=
set SERVICECONTROL_AUDIT_FORWARDEDHEADERS_TRUSTALLPROXIES=
set SERVICECONTROL_AUDIT_FORWARDEDHEADERS_KNOWNPROXIES=
set SERVICECONTROL_AUDIT_FORWARDEDHEADERS_KNOWNNETWORKS=

rem ServiceControl.Monitoring
set MONITORING_FORWARDEDHEADERS_ENABLED=
set MONITORING_FORWARDEDHEADERS_TRUSTALLPROXIES=
set MONITORING_FORWARDEDHEADERS_KNOWNPROXIES=
set MONITORING_FORWARDEDHEADERS_KNOWNNETWORKS=

dotnet run
```

### Scenario 0: Direct Access (No Proxy)

Test a direct request without any forwarded headers, simulating access without a reverse proxy.

**Test with curl (no forwarded headers):**

```cmd
rem ServiceControl (Primary)
curl http://localhost:33333/debug/request-info | json

rem ServiceControl.Audit
curl http://localhost:44444/debug/request-info | json

rem ServiceControl.Monitoring
curl http://localhost:33633/debug/request-info | json
```

**Expected output:**

```json
{
  "processed": {
    "scheme": "http",
    "host": "localhost:33333", // localhost:44444 or localhost:33633
    "remoteIpAddress": "::1"
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

When no forwarded headers are sent, the request values remain unchanged.

### Scenario 1: Default Behavior (With Headers)

Test the default behavior when no forwarded headers environment variables are set, but headers are sent.

**Test with curl (using Group A configuration above):**

```cmd
rem ServiceControl (Primary)
curl -H "X-Forwarded-Proto: https" -H "X-Forwarded-Host: example.com" -H "X-Forwarded-For: 203.0.113.50" http://localhost:33333/debug/request-info | json

rem ServiceControl.Audit
curl -H "X-Forwarded-Proto: https" -H "X-Forwarded-Host: example.com" -H "X-Forwarded-For: 203.0.113.50" http://localhost:44444/debug/request-info | json

rem ServiceControl.Monitoring
curl -H "X-Forwarded-Proto: https" -H "X-Forwarded-Host: example.com" -H "X-Forwarded-For: 203.0.113.50" http://localhost:33633/debug/request-info | json
```

**Expected output:**

```json
{
  "processed": {
    "scheme": "https",
    "host": "example.com",
    "remoteIpAddress": "203.0.113.50"
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

By default, forwarded headers are **enabled** and **all proxies are trusted**. This means any client can spoof `X-Forwarded-*` headers. This is suitable for development but should be restricted in production by configuring `KnownProxies` or `KnownNetworks`.

### Scenario 2: Trust All Proxies (Explicit)

Explicitly enable trust all proxies (same as default, but explicit configuration). This scenario can be tested with the same Group A configuration - the behavior is identical.

**Test with curl (using Group A configuration above):**

```cmd
rem ServiceControl (Primary)
curl -H "X-Forwarded-Proto: https" -H "X-Forwarded-Host: example.com" -H "X-Forwarded-For: 203.0.113.50" http://localhost:33333/debug/request-info | json

rem ServiceControl.Audit
curl -H "X-Forwarded-Proto: https" -H "X-Forwarded-Host: example.com" -H "X-Forwarded-For: 203.0.113.50" http://localhost:44444/debug/request-info | json

rem ServiceControl.Monitoring
curl -H "X-Forwarded-Proto: https" -H "X-Forwarded-Host: example.com" -H "X-Forwarded-For: 203.0.113.50" http://localhost:33633/debug/request-info | json
```

**Expected output:**

```json
{
  "processed": {
    "scheme": "https",
    "host": "example.com",
    "remoteIpAddress": "203.0.113.50"
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

The `scheme` is `https` (from `X-Forwarded-Proto`), `host` is `example.com` (from `X-Forwarded-Host`), and `remoteIpAddress` is `203.0.113.50` (from `X-Forwarded-For`) because all proxies are trusted. The `rawHeaders` are empty because the middleware consumed them.

### Scenario 8: Proxy Chain (Multiple X-Forwarded-For Values)

Test how ServiceControl handles multiple proxies in the chain.

**Test with curl (using Group A configuration above, simulating a proxy chain):**

```cmd
rem ServiceControl (Primary)
curl -H "X-Forwarded-Proto: https" -H "X-Forwarded-Host: example.com" -H "X-Forwarded-For: 203.0.113.50, 10.0.0.1, 192.168.1.1" http://localhost:33333/debug/request-info | json

rem ServiceControl.Audit
curl -H "X-Forwarded-Proto: https" -H "X-Forwarded-Host: example.com" -H "X-Forwarded-For: 203.0.113.50, 10.0.0.1, 192.168.1.1" http://localhost:44444/debug/request-info | json

rem ServiceControl.Monitoring
curl -H "X-Forwarded-Proto: https" -H "X-Forwarded-Host: example.com" -H "X-Forwarded-For: 203.0.113.50, 10.0.0.1, 192.168.1.1" http://localhost:33633/debug/request-info | json
```

**Expected output:**

```json
{
  "processed": {
    "scheme": "https",
    "host": "example.com",
    "remoteIpAddress": "203.0.113.50"
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

The `X-Forwarded-For` header contains multiple IPs representing the proxy chain. When `TrustAllProxies` is `true`, `ForwardLimit` is set to `null` (no limit), so the middleware processes all IPs and returns the original client IP (`203.0.113.50`).

### Scenario 11: Partial Headers (Proto Only)

Test that each forwarded header is processed independently. Only sending `X-Forwarded-Proto` should update the scheme while leaving host and remoteIpAddress unchanged.

**Test with curl (using Group A configuration above, only X-Forwarded-Proto):**

```cmd
rem ServiceControl (Primary)
curl -H "X-Forwarded-Proto: https" http://localhost:33333/debug/request-info | json

rem ServiceControl.Audit
curl -H "X-Forwarded-Proto: https" http://localhost:44444/debug/request-info | json

rem ServiceControl.Monitoring
curl -H "X-Forwarded-Proto: https" http://localhost:33633/debug/request-info | json
```

**Expected output:**

```json
{
  "processed": {
    "scheme": "https",
    "host": "localhost:33333",
    "remoteIpAddress": "::1"
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

Only the `scheme` changed to `https`. The `host` remains `localhost:33333` and `remoteIpAddress` remains `::1` because those headers weren't sent. Each header is processed independently.

### Scenario 13: Multiple X-Forwarded-Proto and X-Forwarded-Host Values

Test how ServiceControl handles multiple values in `X-Forwarded-Proto` and `X-Forwarded-Host` headers, which can occur in multi-proxy environments where each proxy adds its own values.

**Test with curl (using Group A configuration above, simulating multiple proxy values):**

```cmd
rem ServiceControl (Primary)
curl -H "X-Forwarded-Proto: https, http" -H "X-Forwarded-Host: example.com, internal.proxy.local" -H "X-Forwarded-For: 203.0.113.50, 10.0.0.1" http://localhost:33333/debug/request-info | json

rem ServiceControl.Audit
curl -H "X-Forwarded-Proto: https, http" -H "X-Forwarded-Host: example.com, internal.proxy.local" -H "X-Forwarded-For: 203.0.113.50, 10.0.0.1" http://localhost:44444/debug/request-info | json

rem ServiceControl.Monitoring
curl -H "X-Forwarded-Proto: https, http" -H "X-Forwarded-Host: example.com, internal.proxy.local" -H "X-Forwarded-For: 203.0.113.50, 10.0.0.1" http://localhost:33633/debug/request-info | json
```

**Expected output:**

```json
{
  "processed": {
    "scheme": "https",
    "host": "example.com",
    "remoteIpAddress": "203.0.113.50"
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

When `TrustAllProxies` is `true`, `ForwardLimit` is set to `null` (no limit), so the middleware processes all values and returns the leftmost (original) values: `scheme` is `https`, `host` is `example.com`, and `remoteIpAddress` is `203.0.113.50`.

---

## Group B: KnownProxies (Localhost) Configuration

**Restart the instance with this configuration, then run all tests in this group (Scenarios 3, 9, 14).**

```cmd
rem ServiceControl (Primary)
set SERVICECONTROL_FORWARDEDHEADERS_ENABLED=true
set SERVICECONTROL_FORWARDEDHEADERS_TRUSTALLPROXIES=
set SERVICECONTROL_FORWARDEDHEADERS_KNOWNPROXIES=127.0.0.1,::1
set SERVICECONTROL_FORWARDEDHEADERS_KNOWNNETWORKS=

rem ServiceControl.Audit
set SERVICECONTROL_AUDIT_FORWARDEDHEADERS_ENABLED=true
set SERVICECONTROL_AUDIT_FORWARDEDHEADERS_TRUSTALLPROXIES=
set SERVICECONTROL_AUDIT_FORWARDEDHEADERS_KNOWNPROXIES=127.0.0.1,::1
set SERVICECONTROL_AUDIT_FORWARDEDHEADERS_KNOWNNETWORKS=

rem ServiceControl.Monitoring
set MONITORING_FORWARDEDHEADERS_ENABLED=true
set MONITORING_FORWARDEDHEADERS_TRUSTALLPROXIES=
set MONITORING_FORWARDEDHEADERS_KNOWNPROXIES=127.0.0.1,::1
set MONITORING_FORWARDEDHEADERS_KNOWNNETWORKS=

dotnet run
```

> [!NOTE]
> Setting `KNOWNPROXIES` automatically disables `TRUSTALLPROXIES`. Both IPv4 (`127.0.0.1`) and IPv6 (`::1`) loopback addresses are included since curl may use either.

### Scenario 3: Known Proxies Only

Only accept forwarded headers from specific IP addresses.

**Test with curl (from localhost - should work):**

```cmd
rem ServiceControl (Primary)
curl -H "X-Forwarded-Proto: https" -H "X-Forwarded-Host: example.com" -H "X-Forwarded-For: 203.0.113.50" http://localhost:33333/debug/request-info | json

rem ServiceControl.Audit
curl -H "X-Forwarded-Proto: https" -H "X-Forwarded-Host: example.com" -H "X-Forwarded-For: 203.0.113.50" http://localhost:44444/debug/request-info | json

rem ServiceControl.Monitoring
curl -H "X-Forwarded-Proto: https" -H "X-Forwarded-Host: example.com" -H "X-Forwarded-For: 203.0.113.50" http://localhost:33633/debug/request-info | json
```

**Expected output:**

```json
{
  "processed": {
    "scheme": "https",
    "host": "example.com",
    "remoteIpAddress": "203.0.113.50"
  },
  "rawHeaders": {
    "xForwardedFor": "",
    "xForwardedProto": "",
    "xForwardedHost": ""
  },
  "configuration": {
    "enabled": true,
    "trustAllProxies": false,
    "knownProxies": ["127.0.0.1", "::1"],
    "knownNetworks": []
  }
}
```

Headers are applied because the request comes from localhost, which is in the known proxies list. The `rawHeaders` are empty because the middleware consumed them.

### Scenario 9: Proxy Chain with Known Proxies (ForwardLimit = 1)

Test how ServiceControl handles multiple proxies when `TrustAllProxies` is `false`. In this case, `ForwardLimit` remains at its default of `1`, so only the last proxy IP is processed.

**Test with curl (using Group B configuration above, simulating a proxy chain):**

```cmd
rem ServiceControl (Primary)
curl -H "X-Forwarded-Proto: https" -H "X-Forwarded-Host: example.com" -H "X-Forwarded-For: 203.0.113.50, 10.0.0.1, 192.168.1.1" http://localhost:33333/debug/request-info | json

rem ServiceControl.Audit
curl -H "X-Forwarded-Proto: https" -H "X-Forwarded-Host: example.com" -H "X-Forwarded-For: 203.0.113.50, 10.0.0.1, 192.168.1.1" http://localhost:44444/debug/request-info | json

rem ServiceControl.Monitoring
curl -H "X-Forwarded-Proto: https" -H "X-Forwarded-Host: example.com" -H "X-Forwarded-For: 203.0.113.50, 10.0.0.1, 192.168.1.1" http://localhost:33633/debug/request-info | json
```

**Expected output:**

```json
{
  "processed": {
    "scheme": "https",
    "host": "example.com",
    "remoteIpAddress": "192.168.1.1"
  },
  "rawHeaders": {
    "xForwardedFor": "203.0.113.50, 10.0.0.1",
    "xForwardedProto": "",
    "xForwardedHost": ""
  },
  "configuration": {
    "enabled": true,
    "trustAllProxies": false,
    "knownProxies": ["127.0.0.1", "::1"],
    "knownNetworks": []
  }
}
```

When `TrustAllProxies` is `false`, `ForwardLimit` remains at its default of `1`. The middleware only processes the rightmost IP from the chain (`192.168.1.1`). The remaining IPs (`203.0.113.50, 10.0.0.1`) stay in the `X-Forwarded-For` header. Compare this to Scenario 8 where `TrustAllProxies = true` returns the original client IP.

### Scenario 14: Multiple Header Values with Known Proxies (ForwardLimit = 1)

Test how ServiceControl handles multiple `X-Forwarded-Proto` and `X-Forwarded-Host` values when `TrustAllProxies` is `false`. In this case, `ForwardLimit` remains at its default of `1`, so only the rightmost value is processed.

**Test with curl (using Group B configuration above, simulating multiple proxy values):**

```cmd
rem ServiceControl (Primary)
curl -H "X-Forwarded-Proto: https, http" -H "X-Forwarded-Host: example.com, internal.proxy.local" -H "X-Forwarded-For: 203.0.113.50, 10.0.0.1" http://localhost:33333/debug/request-info | json

rem ServiceControl.Audit
curl -H "X-Forwarded-Proto: https, http" -H "X-Forwarded-Host: example.com, internal.proxy.local" -H "X-Forwarded-For: 203.0.113.50, 10.0.0.1" http://localhost:44444/debug/request-info | json

rem ServiceControl.Monitoring
curl -H "X-Forwarded-Proto: https, http" -H "X-Forwarded-Host: example.com, internal.proxy.local" -H "X-Forwarded-For: 203.0.113.50, 10.0.0.1" http://localhost:33633/debug/request-info | json
```

**Expected output:**

```json
{
  "processed": {
    "scheme": "http",
    "host": "internal.proxy.local",
    "remoteIpAddress": "10.0.0.1"
  },
  "rawHeaders": {
    "xForwardedFor": "203.0.113.50",
    "xForwardedProto": "https",
    "xForwardedHost": "example.com"
  },
  "configuration": {
    "enabled": true,
    "trustAllProxies": false,
    "knownProxies": ["127.0.0.1", "::1"],
    "knownNetworks": []
  }
}
```

When `TrustAllProxies` is `false`, `ForwardLimit` remains at its default of `1`. The middleware only processes the rightmost value from each header: `scheme` is `http`, `host` is `internal.proxy.local`, and `remoteIpAddress` is `10.0.0.1`. The remaining values stay in the raw headers. Compare this to Scenario 13 where `TrustAllProxies = true` returns the original (leftmost) values.

---

## Group C: KnownNetworks (Localhost) Configuration

**Restart the instance with this configuration (Scenario 4).**

```cmd
rem ServiceControl (Primary)
set SERVICECONTROL_FORWARDEDHEADERS_ENABLED=true
set SERVICECONTROL_FORWARDEDHEADERS_TRUSTALLPROXIES=
set SERVICECONTROL_FORWARDEDHEADERS_KNOWNPROXIES=
set SERVICECONTROL_FORWARDEDHEADERS_KNOWNNETWORKS=127.0.0.0/8,::1/128

rem ServiceControl.Audit
set SERVICECONTROL_AUDIT_FORWARDEDHEADERS_ENABLED=true
set SERVICECONTROL_AUDIT_FORWARDEDHEADERS_TRUSTALLPROXIES=
set SERVICECONTROL_AUDIT_FORWARDEDHEADERS_KNOWNPROXIES=
set SERVICECONTROL_AUDIT_FORWARDEDHEADERS_KNOWNNETWORKS=127.0.0.0/8,::1/128

rem ServiceControl.Monitoring
set MONITORING_FORWARDEDHEADERS_ENABLED=true
set MONITORING_FORWARDEDHEADERS_TRUSTALLPROXIES=
set MONITORING_FORWARDEDHEADERS_KNOWNPROXIES=
set MONITORING_FORWARDEDHEADERS_KNOWNNETWORKS=127.0.0.0/8,::1/128

dotnet run
```

> [!NOTE]
> Both IPv4 (`127.0.0.0/8`) and IPv6 (`::1/128`) loopback networks are included since curl may use either.

### Scenario 4: Known Networks (CIDR)

Trust all proxies within a network range.

**Test with curl:**

```cmd
rem ServiceControl (Primary)
curl -H "X-Forwarded-Proto: https" -H "X-Forwarded-Host: example.com" -H "X-Forwarded-For: 203.0.113.50" http://localhost:33333/debug/request-info | json

rem ServiceControl.Audit
curl -H "X-Forwarded-Proto: https" -H "X-Forwarded-Host: example.com" -H "X-Forwarded-For: 203.0.113.50" http://localhost:44444/debug/request-info | json

rem ServiceControl.Monitoring
curl -H "X-Forwarded-Proto: https" -H "X-Forwarded-Host: example.com" -H "X-Forwarded-For: 203.0.113.50" http://localhost:33633/debug/request-info | json
```

**Expected output:**

```json
{
  "processed": {
    "scheme": "https",
    "host": "example.com",
    "remoteIpAddress": "203.0.113.50"
  },
  "rawHeaders": {
    "xForwardedFor": "",
    "xForwardedProto": "",
    "xForwardedHost": ""
  },
  "configuration": {
    "enabled": true,
    "trustAllProxies": false,
    "knownProxies": [],
    "knownNetworks": ["127.0.0.0/8", "::1/128"]
  }
}
```

Headers are applied because the request comes from localhost, which falls within the known networks. The `rawHeaders` are empty because the middleware consumed them.

---

## Group D: Untrusted Proxy Configuration

**Restart the instance with this configuration (Scenario 5).**

```cmd
rem ServiceControl (Primary)
set SERVICECONTROL_FORWARDEDHEADERS_ENABLED=true
set SERVICECONTROL_FORWARDEDHEADERS_TRUSTALLPROXIES=
set SERVICECONTROL_FORWARDEDHEADERS_KNOWNPROXIES=192.168.1.100
set SERVICECONTROL_FORWARDEDHEADERS_KNOWNNETWORKS=

rem ServiceControl.Audit
set SERVICECONTROL_AUDIT_FORWARDEDHEADERS_ENABLED=true
set SERVICECONTROL_AUDIT_FORWARDEDHEADERS_TRUSTALLPROXIES=
set SERVICECONTROL_AUDIT_FORWARDEDHEADERS_KNOWNPROXIES=192.168.1.100
set SERVICECONTROL_AUDIT_FORWARDEDHEADERS_KNOWNNETWORKS=

rem ServiceControl.Monitoring
set MONITORING_FORWARDEDHEADERS_ENABLED=true
set MONITORING_FORWARDEDHEADERS_TRUSTALLPROXIES=
set MONITORING_FORWARDEDHEADERS_KNOWNPROXIES=192.168.1.100
set MONITORING_FORWARDEDHEADERS_KNOWNNETWORKS=

dotnet run
```

### Scenario 5: Unknown Proxy Rejected

Configure a known proxy that doesn't match the request source to verify headers are ignored.

**Test with curl:**

```cmd
rem ServiceControl (Primary)
curl -H "X-Forwarded-Proto: https" -H "X-Forwarded-Host: example.com" -H "X-Forwarded-For: 203.0.113.50" http://localhost:33333/debug/request-info | json

rem ServiceControl.Audit
curl -H "X-Forwarded-Proto: https" -H "X-Forwarded-Host: example.com" -H "X-Forwarded-For: 203.0.113.50" http://localhost:44444/debug/request-info | json

rem ServiceControl.Monitoring
curl -H "X-Forwarded-Proto: https" -H "X-Forwarded-Host: example.com" -H "X-Forwarded-For: 203.0.113.50" http://localhost:33633/debug/request-info | json
```

**Expected output:**

```json
{
  "processed": {
    "scheme": "http",
    "host": "localhost:33333",
    "remoteIpAddress": "::1"
  },
  "rawHeaders": {
    "xForwardedFor": "203.0.113.50",
    "xForwardedProto": "https",
    "xForwardedHost": "example.com"
  },
  "configuration": {
    "enabled": true,
    "trustAllProxies": false,
    "knownProxies": ["192.168.1.100"],
    "knownNetworks": []
  }
}
```

Headers are **ignored** because the request comes from localhost (`::1`), which is NOT in the known proxies list (`192.168.1.100`). Notice `scheme` is `http` (unchanged from original request). The `rawHeaders` still show the headers that were sent but not applied.

---

## Group E: Untrusted Network Configuration

**Restart the instance with this configuration (Scenario 6).**

```cmd
rem ServiceControl (Primary)
set SERVICECONTROL_FORWARDEDHEADERS_ENABLED=true
set SERVICECONTROL_FORWARDEDHEADERS_TRUSTALLPROXIES=
set SERVICECONTROL_FORWARDEDHEADERS_KNOWNPROXIES=
set SERVICECONTROL_FORWARDEDHEADERS_KNOWNNETWORKS=10.0.0.0/8,192.168.0.0/16

rem ServiceControl.Audit
set SERVICECONTROL_AUDIT_FORWARDEDHEADERS_ENABLED=true
set SERVICECONTROL_AUDIT_FORWARDEDHEADERS_TRUSTALLPROXIES=
set SERVICECONTROL_AUDIT_FORWARDEDHEADERS_KNOWNPROXIES=
set SERVICECONTROL_AUDIT_FORWARDEDHEADERS_KNOWNNETWORKS=10.0.0.0/8,192.168.0.0/16

rem ServiceControl.Monitoring
set MONITORING_FORWARDEDHEADERS_ENABLED=true
set MONITORING_FORWARDEDHEADERS_TRUSTALLPROXIES=
set MONITORING_FORWARDEDHEADERS_KNOWNPROXIES=
set MONITORING_FORWARDEDHEADERS_KNOWNNETWORKS=10.0.0.0/8,192.168.0.0/16

dotnet run
```

### Scenario 6: Unknown Network Rejected

Configure a known network that doesn't match the request source to verify headers are ignored.

**Test with curl:**

```cmd
rem ServiceControl (Primary)
curl -H "X-Forwarded-Proto: https" -H "X-Forwarded-Host: example.com" -H "X-Forwarded-For: 203.0.113.50" http://localhost:33333/debug/request-info | json

rem ServiceControl.Audit
curl -H "X-Forwarded-Proto: https" -H "X-Forwarded-Host: example.com" -H "X-Forwarded-For: 203.0.113.50" http://localhost:44444/debug/request-info | json

rem ServiceControl.Monitoring
curl -H "X-Forwarded-Proto: https" -H "X-Forwarded-Host: example.com" -H "X-Forwarded-For: 203.0.113.50" http://localhost:33633/debug/request-info | json
```

**Expected output:**

```json
{
  "processed": {
    "scheme": "http",
    "host": "localhost:33333",
    "remoteIpAddress": "::1"
  },
  "rawHeaders": {
    "xForwardedFor": "203.0.113.50",
    "xForwardedProto": "https",
    "xForwardedHost": "example.com"
  },
  "configuration": {
    "enabled": true,
    "trustAllProxies": false,
    "knownProxies": [],
    "knownNetworks": ["10.0.0.0/8", "192.168.0.0/16"]
  }
}
```

Headers are **ignored** because the request comes from localhost (`::1`), which is NOT in the known networks (`10.0.0.0/8` or `192.168.0.0/16`). Notice `scheme` is `http` (unchanged from original request). The `rawHeaders` still show the headers that were sent but not applied.

---

## Group F: Disabled Configuration

**Restart the instance with this configuration (Scenario 7).**

```cmd
rem ServiceControl (Primary)
set SERVICECONTROL_FORWARDEDHEADERS_ENABLED=false
set SERVICECONTROL_FORWARDEDHEADERS_TRUSTALLPROXIES=
set SERVICECONTROL_FORWARDEDHEADERS_KNOWNPROXIES=
set SERVICECONTROL_FORWARDEDHEADERS_KNOWNNETWORKS=

rem ServiceControl.Audit
set SERVICECONTROL_AUDIT_FORWARDEDHEADERS_ENABLED=false
set SERVICECONTROL_AUDIT_FORWARDEDHEADERS_TRUSTALLPROXIES=
set SERVICECONTROL_AUDIT_FORWARDEDHEADERS_KNOWNPROXIES=
set SERVICECONTROL_AUDIT_FORWARDEDHEADERS_KNOWNNETWORKS=

rem ServiceControl.Monitoring
set MONITORING_FORWARDEDHEADERS_ENABLED=false
set MONITORING_FORWARDEDHEADERS_TRUSTALLPROXIES=
set MONITORING_FORWARDEDHEADERS_KNOWNPROXIES=
set MONITORING_FORWARDEDHEADERS_KNOWNNETWORKS=

dotnet run
```

### Scenario 7: Forwarded Headers Disabled

Completely disable forwarded headers processing.

**Test with curl:**

```cmd
rem ServiceControl (Primary)
curl -H "X-Forwarded-Proto: https" -H "X-Forwarded-Host: example.com" -H "X-Forwarded-For: 203.0.113.50" http://localhost:33333/debug/request-info | json

rem ServiceControl.Audit
curl -H "X-Forwarded-Proto: https" -H "X-Forwarded-Host: example.com" -H "X-Forwarded-For: 203.0.113.50" http://localhost:44444/debug/request-info | json

rem ServiceControl.Monitoring
curl -H "X-Forwarded-Proto: https" -H "X-Forwarded-Host: example.com" -H "X-Forwarded-For: 203.0.113.50" http://localhost:33633/debug/request-info | json
```

**Expected output:**

```json
{
  "processed": {
    "scheme": "http",
    "host": "localhost:33333",
    "remoteIpAddress": "::1"
  },
  "rawHeaders": {
    "xForwardedFor": "203.0.113.50",
    "xForwardedProto": "https",
    "xForwardedHost": "example.com"
  },
  "configuration": {
    "enabled": false,
    "trustAllProxies": false,
    "knownProxies": [],
    "knownNetworks": []
  }
}
```

Headers are ignored because forwarded headers processing is disabled entirely. Notice `enabled` is `false` in the configuration.

---

## Group G: Combined Proxies and Networks Configuration

**Restart the instance with this configuration (Scenario 10).**

```cmd
rem ServiceControl (Primary)
set SERVICECONTROL_FORWARDEDHEADERS_ENABLED=true
set SERVICECONTROL_FORWARDEDHEADERS_TRUSTALLPROXIES=
set SERVICECONTROL_FORWARDEDHEADERS_KNOWNPROXIES=192.168.1.100
set SERVICECONTROL_FORWARDEDHEADERS_KNOWNNETWORKS=127.0.0.0/8,::1/128

rem ServiceControl.Audit
set SERVICECONTROL_AUDIT_FORWARDEDHEADERS_ENABLED=true
set SERVICECONTROL_AUDIT_FORWARDEDHEADERS_TRUSTALLPROXIES=
set SERVICECONTROL_AUDIT_FORWARDEDHEADERS_KNOWNPROXIES=192.168.1.100
set SERVICECONTROL_AUDIT_FORWARDEDHEADERS_KNOWNNETWORKS=127.0.0.0/8,::1/128

rem ServiceControl.Monitoring
set MONITORING_FORWARDEDHEADERS_ENABLED=true
set MONITORING_FORWARDEDHEADERS_TRUSTALLPROXIES=
set MONITORING_FORWARDEDHEADERS_KNOWNPROXIES=192.168.1.100
set MONITORING_FORWARDEDHEADERS_KNOWNNETWORKS=127.0.0.0/8,::1/128

dotnet run
```

### Scenario 10: Combined Known Proxies and Networks

Test using both `KnownProxies` and `KnownNetworks` together.

**Test with curl:**

```cmd
rem ServiceControl (Primary)
curl -H "X-Forwarded-Proto: https" -H "X-Forwarded-Host: example.com" -H "X-Forwarded-For: 203.0.113.50" http://localhost:33333/debug/request-info | json

rem ServiceControl.Audit
curl -H "X-Forwarded-Proto: https" -H "X-Forwarded-Host: example.com" -H "X-Forwarded-For: 203.0.113.50" http://localhost:44444/debug/request-info | json

rem ServiceControl.Monitoring
curl -H "X-Forwarded-Proto: https" -H "X-Forwarded-Host: example.com" -H "X-Forwarded-For: 203.0.113.50" http://localhost:33633/debug/request-info | json
```

**Expected output:**

```json
{
  "processed": {
    "scheme": "https",
    "host": "example.com",
    "remoteIpAddress": "203.0.113.50"
  },
  "rawHeaders": {
    "xForwardedFor": "",
    "xForwardedProto": "",
    "xForwardedHost": ""
  },
  "configuration": {
    "enabled": true,
    "trustAllProxies": false,
    "knownProxies": ["192.168.1.100"],
    "knownNetworks": ["127.0.0.0/8", "::1/128"]
  }
}
```

Headers are applied because the request comes from localhost (`::1`), which falls within the `::1/128` network even though it's not in the `knownProxies` list.

---

## Group H: IPv4 Only Configuration

**Restart the instance with this configuration (Scenario 12).**

```cmd
rem ServiceControl (Primary)
set SERVICECONTROL_FORWARDEDHEADERS_ENABLED=true
set SERVICECONTROL_FORWARDEDHEADERS_TRUSTALLPROXIES=
set SERVICECONTROL_FORWARDEDHEADERS_KNOWNPROXIES=127.0.0.1
set SERVICECONTROL_FORWARDEDHEADERS_KNOWNNETWORKS=

rem ServiceControl.Audit
set SERVICECONTROL_AUDIT_FORWARDEDHEADERS_ENABLED=true
set SERVICECONTROL_AUDIT_FORWARDEDHEADERS_TRUSTALLPROXIES=
set SERVICECONTROL_AUDIT_FORWARDEDHEADERS_KNOWNPROXIES=127.0.0.1
set SERVICECONTROL_AUDIT_FORWARDEDHEADERS_KNOWNNETWORKS=

rem ServiceControl.Monitoring
set MONITORING_FORWARDEDHEADERS_ENABLED=true
set MONITORING_FORWARDEDHEADERS_TRUSTALLPROXIES=
set MONITORING_FORWARDEDHEADERS_KNOWNPROXIES=127.0.0.1
set MONITORING_FORWARDEDHEADERS_KNOWNNETWORKS=

dotnet run
```

> [!NOTE]
> Only IPv4 `127.0.0.1` is configured, not IPv6 `::1`.

### Scenario 12: IPv4/IPv6 Mismatch

Demonstrates a common misconfiguration where only IPv4 localhost is configured but curl uses IPv6. This scenario shows why you should include both `127.0.0.1` and `::1` in your configuration.

**Test with curl:**

```cmd
rem ServiceControl (Primary)
curl -H "X-Forwarded-Proto: https" -H "X-Forwarded-Host: example.com" -H "X-Forwarded-For: 203.0.113.50" http://localhost:33333/debug/request-info | json

rem ServiceControl.Audit
curl -H "X-Forwarded-Proto: https" -H "X-Forwarded-Host: example.com" -H "X-Forwarded-For: 203.0.113.50" http://localhost:44444/debug/request-info | json

rem ServiceControl.Monitoring
curl -H "X-Forwarded-Proto: https" -H "X-Forwarded-Host: example.com" -H "X-Forwarded-For: 203.0.113.50" http://localhost:33633/debug/request-info | json
```

**Expected output (if curl uses IPv6):**

```json
{
  "processed": {
    "scheme": "http",
    "host": "localhost:33333",
    "remoteIpAddress": "::1"
  },
  "rawHeaders": {
    "xForwardedFor": "203.0.113.50",
    "xForwardedProto": "https",
    "xForwardedHost": "example.com"
  },
  "configuration": {
    "enabled": true,
    "trustAllProxies": false,
    "knownProxies": ["127.0.0.1"],
    "knownNetworks": []
  }
}
```

Headers are **ignored** because the request comes from `::1` (IPv6), but only `127.0.0.1` (IPv4) is in the known proxies list. This is a common gotcha - always include both IPv4 and IPv6 loopback addresses when testing locally, or use CIDR notation like `127.0.0.0/8` and `::1/128`.

> [!NOTE]
> If your output shows headers were applied, curl is using IPv4. The behavior depends on your system's DNS resolution for `localhost`.

## Debug Endpoint

The `/debug/request-info` endpoint is only available in Development environment. It returns:

```json
{
  "processed": {
    "scheme": "https",
    "host": "example.com",
    "remoteIpAddress": "203.0.113.50"
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

| Section         | Field             | Description                                                      |
|-----------------|-------------------|------------------------------------------------------------------|
| `processed`     | `scheme`          | The request scheme after forwarded headers processing            |
| `processed`     | `host`            | The request host after forwarded headers processing              |
| `processed`     | `remoteIpAddress` | The client IP after forwarded headers processing                 |
| `rawHeaders`    | `xForwardedFor`   | Raw `X-Forwarded-For` header (empty if consumed by middleware)   |
| `rawHeaders`    | `xForwardedProto` | Raw `X-Forwarded-Proto` header (empty if consumed by middleware) |
| `rawHeaders`    | `xForwardedHost`  | Raw `X-Forwarded-Host` header (empty if consumed by middleware)  |
| `configuration` | `enabled`         | Whether forwarded headers middleware is enabled                  |
| `configuration` | `trustAllProxies` | Whether all proxies are trusted (security warning if true)       |
| `configuration` | `knownProxies`    | List of trusted proxy IP addresses                               |
| `configuration` | `knownNetworks`   | List of trusted CIDR network ranges                              |

### Key Diagnostic Questions

1. **Were headers applied?** - If `rawHeaders` are empty but `processed` values changed, the middleware consumed and applied them
2. **Why weren't headers applied?** - If `rawHeaders` still contain values, the middleware didn't trust the caller. Check `knownProxies` and `knownNetworks` in `configuration`
3. **Is forwarded headers enabled?** - Check `configuration.enabled`

## Cleanup

After testing, clear the environment variables:

**Command Prompt (cmd):**

```cmd
rem ServiceControl (Primary)
set SERVICECONTROL_FORWARDEDHEADERS_ENABLED=
set SERVICECONTROL_FORWARDEDHEADERS_TRUSTALLPROXIES=
set SERVICECONTROL_FORWARDEDHEADERS_KNOWNPROXIES=
set SERVICECONTROL_FORWARDEDHEADERS_KNOWNNETWORKS=

rem ServiceControl.Audit
set SERVICECONTROL_AUDIT_FORWARDEDHEADERS_ENABLED=
set SERVICECONTROL_AUDIT_FORWARDEDHEADERS_TRUSTALLPROXIES=
set SERVICECONTROL_AUDIT_FORWARDEDHEADERS_KNOWNPROXIES=
set SERVICECONTROL_AUDIT_FORWARDEDHEADERS_KNOWNNETWORKS=

rem ServiceControl.Monitoring
set MONITORING_FORWARDEDHEADERS_ENABLED=
set MONITORING_FORWARDEDHEADERS_TRUSTALLPROXIES=
set MONITORING_FORWARDEDHEADERS_KNOWNPROXIES=
set MONITORING_FORWARDEDHEADERS_KNOWNNETWORKS=
```

## Unit Tests

Unit tests for the `ForwardedHeadersSettings` configuration class are located at:

```text
src/ServiceControl.UnitTests/Infrastructure/Settings/ForwardedHeadersSettingsTests.cs
```

## Acceptance Tests

Acceptance tests for end-to-end forwarded headers behavior are located at:

```text
src/ServiceControl.AcceptanceTests/Security/ForwardedHeaders/
src/ServiceControl.Audit.AcceptanceTests/Security/ForwardedHeaders/
src/ServiceControl.Monitoring.AcceptanceTests/Security/ForwardedHeaders/
```

> [!NOTE]
> Scenario 12 (IPv4/IPv6 Mismatch) is not covered by acceptance tests because the test server's IP address (IPv4 vs IPv6) cannot be controlled reliably. The "untrusted proxy" behavior is already validated by Scenarios 5 and 6.

## See Also

- [Hosting Guide](https://docs.particular.net/servicecontrol/security/hosting-guide) - Configuration reference for forwarded headers
- [Reverse Proxy Testing](reverseproxy-testing.md) - Testing with a real reverse proxy (NGINX)
- [Testing Architecture](testing-architecture.md) - Overview of testing patterns in this repository
