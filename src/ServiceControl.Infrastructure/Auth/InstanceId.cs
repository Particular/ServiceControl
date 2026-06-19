#nullable enable
namespace ServiceControl.Infrastructure.Auth;

/// <summary>
/// The ServiceControl instance a permission belongs to. Each instance is a separate process and
/// namespaces its permissions with this prefix. The wire value is the name lowercased
/// (e.g. <see cref="Error"/> → <c>error</c>).
/// </summary>
public enum InstanceId
{
    Error,
    Audit,
    Monitoring
}
