namespace ServiceControl.Infrastructure.Api;

using System;
using ServiceControl.Persistence;

/// <summary>
/// Compares this instance's identity hashes with what an audit remote served and reduces them to
/// the classifications the usage report carries. Only these results are ever reported; the hashes
/// stay between the instances.
/// </summary>
static class AuditSharingClassifier
{
    public const string Unknown = "Unknown";
    public const string NotApplicable = "NotApplicable";

    public static string SameMachine(string localMachineNameHash, string remoteMachineNameHash)
    {
        if (localMachineNameHash is null || remoteMachineNameHash is null)
        {
            return Unknown;
        }

        if (localMachineNameHash == MachineIdentity.NotApplicable || remoteMachineNameHash == MachineIdentity.NotApplicable)
        {
            return NotApplicable;
        }

        return string.Equals(localMachineNameHash, remoteMachineNameHash, StringComparison.Ordinal) ? "True" : "False";
    }

    public static string DatabaseSharing(HashedStorageIdentity local, HashedStorageIdentity remote)
    {
        if (local is null || remote is null)
        {
            return Unknown;
        }

        if (!string.Equals(local.Engine, remote.Engine, StringComparison.OrdinalIgnoreCase))
        {
            return NotApplicable;
        }

        if (!string.Equals(local.ServerHash, remote.ServerHash, StringComparison.Ordinal))
        {
            return "SeparateServer";
        }

        if (!string.Equals(local.DatabaseHash, remote.DatabaseHash, StringComparison.Ordinal))
        {
            return "SameServer";
        }

        if (local.SchemaHash is not null && string.Equals(local.SchemaHash, remote.SchemaHash, StringComparison.Ordinal))
        {
            return "SameSchema";
        }

        return "SameDatabase";
    }

    /// <summary>
    /// What the report counts once when summing sizes and counts. Instances writing to the same
    /// schema share one store; instances in different schemas of one database each have their own.
    /// </summary>
    public static string StoreKey(HashedStorageIdentity identity) =>
        identity.SchemaHash is null
            ? $"{identity.ServerHash}/{identity.DatabaseHash}"
            : $"{identity.ServerHash}/{identity.DatabaseHash}/{identity.SchemaHash}";

    public static HashedStorageIdentity Hash(StorageIdentity identity) =>
        identity is null
            ? null
            : new HashedStorageIdentity(
                identity.Engine,
                IdentityHash.Compute(identity.Server),
                IdentityHash.Compute(identity.Database),
                identity.Schema is null ? null : IdentityHash.Compute(identity.Schema));
}

record HashedStorageIdentity(string Engine, string ServerHash, string DatabaseHash, string SchemaHash);
