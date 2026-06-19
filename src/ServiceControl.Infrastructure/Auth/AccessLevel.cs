#nullable enable
namespace ServiceControl.Infrastructure.Auth;

/// <summary>
/// The action a permission authorizes. <see cref="View"/> is the read-only level; every other value is a
/// write/mutating action. The wire value is the name lowercased (e.g. <see cref="View"/> → <c>view</c>).
/// </summary>
public enum AccessLevel
{
    View,
    Retry,
    Archive,
    Unarchive,
    Edit,
    Manage,
    Delete,
    Test
}
