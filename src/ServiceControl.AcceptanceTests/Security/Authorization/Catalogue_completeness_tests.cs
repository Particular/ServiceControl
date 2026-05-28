#nullable enable
namespace ServiceControl.AcceptanceTests.Security.Authorization;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using NUnit.Framework;
using ServiceControl.Connection;
using ServiceControl.Infrastructure.Auth.Rbac;

/// <summary>
/// Fast unit-style tests (no host startup) that keep the permission catalogue honest.
/// <para>
/// Two invariants are asserted:
/// <list type="number">
///   <item>
///     Every constant in <see cref="Permissions.All"/> (except <c>*</c>) is either enforced
///     via an <c>[Authorize(Policy = X)]</c> attribute on a controller action (where <c>X</c> is
///     a member of <see cref="Permissions.All"/>), or explicitly declared as unenforced in
///     <see cref="KnownUnenforcedPermissions.Set"/>.
///     Failing this means a new constant was added without enforcement or a known-unenforced entry.
///   </item>
///   <item>
///     Every entry in <see cref="KnownUnenforcedPermissions.Set"/> is genuinely unenforced.
///     Failing this means enforcement was added but the known-unenforced entry was not removed.
///   </item>
/// </list>
/// </para>
/// <para>
/// Assembly walk strategy:
/// <list type="bullet">
///   <item>
///     All controllers live in the main ServiceControl assembly, anchored via
///     <see cref="ConnectionController"/> (a stable, non-auth-specific controller type).
///   </item>
///   <item>
///     <see cref="Permissions"/> and <see cref="KnownUnenforcedPermissions"/> live in the
///     Infrastructure assembly, anchored via <see cref="Permissions"/>.
///   </item>
/// </list>
/// All assemblies scanned are already referenced by the test project; no runtime loading is needed.
/// </para>
/// </summary>
[TestFixture]
public class Catalogue_completeness_tests
{
    /// <summary>
    /// Collects all permission strings that appear as the <c>Policy</c> of an
    /// <c>[Authorize]</c> attribute on any controller class or action method across the
    /// ServiceControl assembly, filtered to only those that are members of <see cref="Permissions.All"/>.
    /// </summary>
    static IReadOnlySet<string> CollectEnforcedPermissions()
    {
        // The main ServiceControl assembly hosts all controllers.
        // Anchored via ConnectionController — a stable, always-present controller type.
        var scAssembly = typeof(ConnectionController).Assembly;

        var enforced = new HashSet<string>(StringComparer.Ordinal);

        foreach (var type in scAssembly.GetExportedTypes())
        {
            // Check class-level [Authorize(Policy = X)]
            foreach (var attr in type.GetCustomAttributes<AuthorizeAttribute>(inherit: true))
            {
                if (attr.Policy != null && Permissions.All.Contains(attr.Policy))
                {
                    enforced.Add(attr.Policy);
                }
            }

            // Check method-level [Authorize(Policy = X)] on all public instance methods
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                foreach (var attr in method.GetCustomAttributes<AuthorizeAttribute>(inherit: true))
                {
                    if (attr.Policy != null && Permissions.All.Contains(attr.Policy))
                    {
                        enforced.Add(attr.Policy);
                    }
                }
            }
        }

        return enforced;
    }

    [Test]
    public void Every_catalogue_constant_is_either_enforced_or_declared_as_known_unenforced()
    {
        var enforced = CollectEnforcedPermissions();

        // All declared permissions except the wildcard grant ("*" is not a real permission)
        var catalogue = Permissions.All
            .Where(p => p != "*")
            .ToHashSet(StringComparer.Ordinal);

        // Unenforced = declared but not yet wired to any controller action
        var unenforced = catalogue
            .Except(enforced)
            .ToHashSet(StringComparer.Ordinal);

        // Any permission that is unenforced AND not listed in KnownUnenforcedPermissions is a gap
        var gaps = unenforced
            .Except(KnownUnenforcedPermissions.Set)
            .OrderBy(p => p)
            .ToList();

        if (gaps.Count > 0)
        {
            Assert.Fail(
                $"The following permission(s) are declared in Permissions but are neither enforced " +
                $"by an [Authorize(Policy = X)] attribute on a controller action nor listed in KnownUnenforcedPermissions.Set.\n" +
                $"Either add [Authorize(Policy = \"{gaps[0]}\")] to the appropriate controller action, " +
                $"or add the constant to KnownUnenforcedPermissions.Set until enforcement is implemented:\n" +
                string.Join("\n", gaps.Select(p => $"  - {p}")));
        }
    }

    [Test]
    public void KnownUnenforced_set_contains_no_stale_entries()
    {
        var enforced = CollectEnforcedPermissions();

        // All declared permissions except the wildcard grant ("*" is not a real permission)
        var catalogue = Permissions.All
            .Where(p => p != "*")
            .ToHashSet(StringComparer.Ordinal);

        // Unenforced = declared but not yet wired to any controller action
        var unenforced = catalogue
            .Except(enforced)
            .ToHashSet(StringComparer.Ordinal);

        // KnownUnenforced entries that are now enforced (stale)
        var stale = KnownUnenforcedPermissions.Set
            .Except(unenforced)
            .OrderBy(p => p)
            .ToList();

        if (stale.Count > 0)
        {
            Assert.Fail(
                $"The following permission(s) are listed in KnownUnenforcedPermissions.Set but are " +
                $"now enforced by an [Authorize(Policy = X)] attribute — remove them from " +
                $"KnownUnenforcedPermissions.Set to keep the set accurate:\n" +
                string.Join("\n", stale.Select(p => $"  - {p}")));
        }
    }
}
