#nullable enable
namespace ServiceControl.Infrastructure.Auth;

using System;

/// <summary>
/// A wildcard pattern over the three permission segments, where a <see langword="null"/> segment is the
/// <c>*</c> wildcard that matches any value. For example <c>*:*:view</c> is
/// <c>new PermissionPattern(null, null, AccessLevel.View)</c> and matches every view permission.
/// </summary>
public readonly record struct PermissionPattern(InstanceId? Instance, Component? Component, AccessLevel? Access)
{
    /// <summary>Returns <see langword="true"/> if <paramref name="permission"/> matches every non-wildcard segment.</summary>
    public bool Matches(PermissionId permission) =>
        (Instance is null || Instance == permission.Instance)
        && (Component is null || Component == permission.Component)
        && (Access is null || Access == permission.Access);

    /// <summary>
    /// Parses a colon-delimited pattern (e.g. <c>*:*:view</c>) where <c>*</c> is a segment wildcard.
    /// Throws on a malformed pattern or an unknown segment value.
    /// </summary>
    public static PermissionPattern Parse(string value)
    {
        var segments = value.Split(':');
        if (segments.Length != 3)
        {
            throw new FormatException($"'{value}' is not a valid permission pattern (expected instance:component:access).");
        }

        return new PermissionPattern(
            ParseSegment<InstanceId>(segments[0]),
            ParseSegment<Component>(segments[1]),
            ParseSegment<AccessLevel>(segments[2]));
    }

    static T? ParseSegment<T>(string value) where T : struct, Enum
    {
        if (value == "*")
        {
            return null;
        }

        if (Enum.TryParse<T>(value, ignoreCase: true, out var result) && Enum.IsDefined(result))
        {
            return result;
        }

        throw new FormatException($"'{value}' is not a valid {typeof(T).Name} segment.");
    }
}
