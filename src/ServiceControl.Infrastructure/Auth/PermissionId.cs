#nullable enable
namespace ServiceControl.Infrastructure.Auth;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// A strongly-typed authorization permission in the canonical wire format
/// <c>instance:component:access</c> (e.g. <c>error:messages:view</c>).
/// <para>
/// The hand-authored <c>const string</c> constants in <see cref="Permissions"/> remain the source of
/// truth (they are required by <c>[Authorize(Policy = …)]</c> attributes). <see cref="All"/> is derived
/// from <see cref="Permissions.All"/>, and <see cref="TryParse"/> only accepts a triple that is a member
/// of that catalogue — so a well-typed but non-existent combination such as <c>audit:messages:retry</c>
/// is rejected.
/// </para>
/// </summary>
public readonly record struct PermissionId(InstanceId Instance, Component Component, AccessLevel Access)
{
    /// <summary>The complete set of known permissions, parsed from <see cref="Permissions.All"/>.</summary>
    public static IReadOnlySet<PermissionId> All { get; } = BuildAll();

    /// <summary>The canonical wire representation, <c>instance:component:access</c> (lowercased).</summary>
    public override string ToString() =>
        $"{Instance.ToString().ToLowerInvariant()}:{Component.ToString().ToLowerInvariant()}:{Access.ToString().ToLowerInvariant()}";

    /// <summary>
    /// Parses a <c>instance:component:access</c> string. Case-insensitive. Returns <see langword="false"/>
    /// for malformed input or for a well-typed triple that is not part of the known catalogue.
    /// </summary>
    public static bool TryParse(string value, out PermissionId permission) =>
        TryParseSegments(value, out permission) && All.Contains(permission);

    /// <summary>Parses a <c>instance:component:access</c> string, throwing on an unknown or malformed value.</summary>
    public static PermissionId Parse(string value) =>
        TryParse(value, out var permission)
            ? permission
            : throw new FormatException($"'{value}' is not a known permission.");

    static IReadOnlySet<PermissionId> BuildAll()
    {
        var set = new HashSet<PermissionId>();
        foreach (var value in Permissions.All)
        {
            if (TryParseSegments(value, out var permission))
            {
                set.Add(permission);
            }
        }

        return set;
    }

    // Enum-level parse only; does not validate the triple against the catalogue (used to build it).
    static bool TryParseSegments(string value, out PermissionId permission)
    {
        permission = default;

        var segments = value.Split(':');
        if (segments.Length != 3)
        {
            return false;
        }

        if (TryParseEnum<InstanceId>(segments[0], out var instance)
            && TryParseEnum<Component>(segments[1], out var component)
            && TryParseEnum<AccessLevel>(segments[2], out var access))
        {
            permission = new PermissionId(instance, component, access);
            return true;
        }

        return false;
    }

    static bool TryParseEnum<T>(string value, out T result) where T : struct, Enum =>
        Enum.TryParse(value, ignoreCase: true, out result) && Enum.IsDefined(result);
}
