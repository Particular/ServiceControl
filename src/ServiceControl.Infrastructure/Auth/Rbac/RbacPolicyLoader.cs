#nullable enable
namespace ServiceControl.Infrastructure.Auth.Rbac;

using System;
using System.Collections.Generic;
using System.IO;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

/// <summary>
/// Loads and parses an RBAC policy from YAML. The file format supports two forms for
/// permission entries:
/// - A bare string: "messages:view"
/// - An object with a scope: { permission: "messages:retry", scope: { allow: [...], deny: [...] } }
/// </summary>
public static class RbacPolicyLoader
{
    static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .WithTypeConverter(new PermissionGrantConverter())
        .Build();

    /// <summary>
    /// Parses an RBAC policy from a YAML string. Throws <see cref="RbacPolicyException"/>
    /// if the YAML is invalid or cannot be deserialized.
    /// </summary>
    public static RbacPolicy Parse(string yaml)
    {
        try
        {
            var dto = Deserializer.Deserialize<RbacPolicyDto>(yaml);
            return MapToPolicy(dto);
        }
        catch (Exception ex) when (ex is not RbacPolicyException)
        {
            throw new RbacPolicyException($"Failed to parse rbac policy: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Loads and parses an RBAC policy from a YAML file at the given path.
    /// </summary>
    public static RbacPolicy LoadFromFile(string path)
    {
        try
        {
            var yaml = File.ReadAllText(path);
            return Parse(yaml);
        }
        catch (RbacPolicyException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new RbacPolicyException($"Failed to load rbac policy from '{path}': {ex.Message}", ex);
        }
    }

    static RbacPolicy MapToPolicy(RbacPolicyDto dto)
    {
        var roles = new Dictionary<string, RbacRole>(StringComparer.Ordinal);
        if (dto.Roles != null)
        {
            foreach (var (key, roleDto) in dto.Roles)
            {
                var bindings = (IReadOnlyList<string>)(roleDto.Bindings ?? []);
                var permissions = new List<PermissionGrant>();
                if (roleDto.Permissions != null)
                {
                    permissions.AddRange(roleDto.Permissions);
                }
                roles[key] = new RbacRole(key, bindings, permissions);
            }
        }
        return new RbacPolicy(dto.SchemaVersion, roles);
    }

    // DTO types used for deserialization only
    class RbacPolicyDto
    {
        public int SchemaVersion { get; set; }
        public Dictionary<string, RbacRoleDto>? Roles { get; set; }
    }

    class RbacRoleDto
    {
        public List<string>? Bindings { get; set; }
        public List<PermissionGrant>? Permissions { get; set; }
    }

    /// <summary>
    /// Handles the polymorphic permission entries: either a bare string or an object
    /// with a "permission" key and optional "scope".
    /// </summary>
    class PermissionGrantConverter : IYamlTypeConverter
    {
        public bool Accepts(Type type) => type == typeof(PermissionGrant);

        public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
        {
            // Bare string form: "messages:view"
            if (parser.TryConsume<Scalar>(out var scalar))
            {
                return new PermissionGrant(scalar.Value, Scope: null);
            }

            // Object form: { permission: "messages:retry", scope: { allow: [...], deny: [...] } }
            parser.Consume<MappingStart>();

            string? permission = null;
            ResourceScopeSpec? scope = null;

            while (!parser.TryConsume<MappingEnd>(out _))
            {
                var key = parser.Consume<Scalar>().Value;
                switch (key)
                {
                    case "permission":
                        permission = parser.Consume<Scalar>().Value;
                        break;
                    case "scope":
                        scope = ReadScope(parser, rootDeserializer);
                        break;
                    default:
                        // Skip unknown keys
                        parser.SkipThisAndNestedEvents();
                        break;
                }
            }

            if (permission == null)
            {
                throw new RbacPolicyException("rbac policy: permission entry missing 'permission' key");
            }

            return new PermissionGrant(permission, scope);
        }

        static ResourceScopeSpec ReadScope(IParser parser, ObjectDeserializer rootDeserializer)
        {
            parser.Consume<MappingStart>();

            List<string>? allow = null;
            List<string>? deny = null;

            while (!parser.TryConsume<MappingEnd>(out _))
            {
                var key = parser.Consume<Scalar>().Value;
                switch (key)
                {
                    case "allow":
                        allow = (List<string>?)rootDeserializer(typeof(List<string>));
                        break;
                    case "deny":
                        deny = (List<string>?)rootDeserializer(typeof(List<string>));
                        break;
                    default:
                        parser.SkipThisAndNestedEvents();
                        break;
                }
            }

            return new ResourceScopeSpec(allow ?? [], deny ?? []);
        }

        public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
            => throw new NotSupportedException("Writing RbacPolicy YAML is not supported.");
    }
}

/// <summary>
/// Thrown when the RBAC policy file cannot be loaded or parsed.
/// The message always contains "rbac" so tests can assert on it.
/// </summary>
public sealed class RbacPolicyException(string message, Exception? inner = null)
    : Exception(message, inner);
