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

    public static string SameMachine(string localMachineIdHash, string remoteMachineIdHash)
    {
        if (localMachineIdHash is null || remoteMachineIdHash is null)
        {
            return Unknown;
        }

        if (localMachineIdHash == MachineIdentity.NotApplicable || remoteMachineIdHash == MachineIdentity.NotApplicable)
        {
            return NotApplicable;
        }

        return string.Equals(localMachineIdHash, remoteMachineIdHash, StringComparison.Ordinal) ? "True" : "False";
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
