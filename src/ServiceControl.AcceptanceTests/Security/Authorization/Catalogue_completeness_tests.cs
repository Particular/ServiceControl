#nullable enable
namespace ServiceControl.AcceptanceTests.Security.Authorization;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ServiceControl.Infrastructure.Auth.Rbac;
using ServiceControl.Infrastructure.WebApi;

/// <summary>
/// Fast unit-style tests (no host startup) that keep the permission catalogue honest.
/// <para>
/// Two invariants are asserted:
/// <list type="number">
///   <item>
///     Every constant in <see cref="Permissions.All"/> (except <c>*</c>) is either enforced
///     via a <c>[RequirePermission]</c> attribute on a controller action, or explicitly declared
///     as unenforced in <see cref="KnownUnenforcedPermissions.Set"/>.
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
///     <see cref="RequirePermissionAttribute"/> and all controllers live in the
///     main ServiceControl assembly, anchored via <see cref="RequirePermissionAttribute"/>.
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
    /// Collects all permissions that appear on at least one <c>[RequirePermission]</c>
    /// attribute on any controller action method across the ServiceControl assembly.
    /// </summary>
    static IReadOnlySet<string> CollectEnforcedPermissions()
    {
        // The main ServiceControl assembly hosts both RequirePermissionAttribute and all controllers.
        var scAssembly = typeof(RequirePermissionAttribute).Assembly;

        var enforced = new HashSet<string>(StringComparer.Ordinal);

        foreach (var type in scAssembly.GetExportedTypes())
        {
            // Check class-level [RequirePermission]
            foreach (var attr in type.GetCustomAttributes<RequirePermissionAttribute>(inherit: true))
            {
                enforced.Add(attr.Permission);
            }

            // Check method-level [RequirePermission] on all public instance methods
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                foreach (var attr in method.GetCustomAttributes<RequirePermissionAttribute>(inherit: true))
                {
                    enforced.Add(attr.Permission);
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
                $"by a [RequirePermission] attribute nor listed in KnownUnenforcedPermissions.Set.\n" +
                $"Either add a [RequirePermission(\"{gaps[0]}\")] to the appropriate controller action, " +
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
                $"now enforced by a [RequirePermission] attribute — remove them from " +
                $"KnownUnenforcedPermissions.Set to keep the set accurate:\n" +
                string.Join("\n", stale.Select(p => $"  - {p}")));
        }
    }
}
