#nullable enable
namespace ServiceControl.Infrastructure.Auth;

/// <summary>The kind of recoverability action being audited. Determines the ECS <c>event.type</c>.</summary>
public enum MessageActionKind
{
    Retry,
    Archive,
    Unarchive
}

/// <summary>How the action selected the messages it acts on.</summary>
public enum MessageActionScope
{
    Single,
    Batch,
    Group,
    Queue,
    Endpoint,
    All,
    Range
}
