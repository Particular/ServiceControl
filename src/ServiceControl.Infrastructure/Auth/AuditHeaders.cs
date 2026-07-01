#nullable enable
namespace ServiceControl.Infrastructure.Auth;

using System.Collections.Generic;
using NServiceBus;

/// <summary>
/// Carries the initiating principal on ServiceControl's own internal command messages so asynchronous
/// handlers can attribute per-message actions. Trusted as-is (trusted-subsystem model): the integrity
/// rests on transport access control, consistent with how the command itself is already trusted. This
/// type is the single stamp/read choke point — cryptographic signing would be added here.
/// </summary>
public static class AuditHeaders
{
    public const string SubjectId = "ServiceControl.Audit.InitiatedBy.Id";
    public const string SubjectName = "ServiceControl.Audit.InitiatedBy.Name";

    public static void Stamp(SendOptions options, AuditUser user)
    {
        options.SetHeader(SubjectId, user.Id);
        options.SetHeader(SubjectName, user.Name);
    }

    public static AuditUser Read(IReadOnlyDictionary<string, string> headers)
    {
        if (headers.TryGetValue(SubjectId, out var id) && !string.IsNullOrEmpty(id))
        {
            headers.TryGetValue(SubjectName, out var name);
            return new AuditUser(id, string.IsNullOrEmpty(name) ? id : name);
        }

        return AuditUser.Anonymous;
    }
}
