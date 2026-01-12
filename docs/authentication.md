# Authentication Configuration

See [ServiceControl Authentication](https://docs.particular.net/servicecontrol/security/configuration/authentication) on the public docs site.

## Anonymous Endpoints

The following endpoints are accessible without authentication, even when authentication is enabled:

| Endpoint                            | Purpose                                                              |
|-------------------------------------|----------------------------------------------------------------------|
| `/api`                              | API root/discovery - returns available endpoints and API information |
| `/api/instance-info`                | Returns instance configuration information                           |
| `/api/configuration`                | Returns instance configuration information (alias)                   |
| `/api/configuration/remotes`        | Returns remote instance configurations for server-to-server fetching |
| `/api/authentication/configuration` | Returns authentication configuration for clients like ServicePulse   |

These endpoints must remain accessible so clients can discover API capabilities and obtain the authentication configuration needed to acquire tokens.

## See Also

- [Forwarded Headers Configuration](forwarded-headers.md) - Configure forwarded headers when behind a reverse proxy
- [HTTPS Configuration](https-configuration.md) - Configure direct HTTPS
