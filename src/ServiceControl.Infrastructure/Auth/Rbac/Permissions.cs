#nullable enable
namespace ServiceControl.Infrastructure.Auth.Rbac;

using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// Catalogue of all known permission constants in the format <c>resource:action</c>.
/// Phase 0 seeds the messages and recoverability set. Later phases extend it.
/// <para>
/// The <see cref="All"/> set is automatically derived from all <c>public const string</c>
/// fields on this class, so adding a new constant is sufficient — no separate registration needed.
/// </para>
/// </summary>
public static class Permissions
{
    // Messages area
    public const string MessagesView = "messages:view";
    public const string MessagesRetry = "messages:retry";
    public const string MessagesArchive = "messages:archive";
    public const string MessagesUnarchive = "messages:unarchive";
    public const string MessagesEdit = "messages:edit";

    // Recoverability groups area
    public const string RecoverabilityGroupsView = "recoverabilitygroups:view";
    public const string RecoverabilityGroupsRetry = "recoverabilitygroups:retry";
    public const string RecoverabilityGroupsArchive = "recoverabilitygroups:archive";
    public const string RecoverabilityGroupsUnarchive = "recoverabilitygroups:unarchive";

    // Endpoints area
    public const string EndpointsView = "endpoints:view";

    // Custom checks area
    public const string CustomChecksView = "customchecks:view";

    /// <summary>
    /// The complete set of known permissions, derived from all <c>public const string</c>
    /// fields declared on this class. Used by tests to assert coverage and by the completeness check.
    /// </summary>
    public static readonly IReadOnlySet<string> All = BuildAll();

    static IReadOnlySet<string> BuildAll()
    {
        var set = new HashSet<string>();
        foreach (var field in typeof(Permissions).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.IsLiteral && !field.IsInitOnly && field.FieldType == typeof(string))
            {
                var value = (string?)field.GetValue(null);
                if (value != null)
                {
                    set.Add(value);
                }
            }
        }
        return set;
    }
}
