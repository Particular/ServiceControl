namespace ServiceControl.Infrastructure.Tests.Auth.Rbac;

using System.Linq;
using System.Security.Claims;
using NUnit.Framework;
using ServiceControl.Infrastructure.Auth.Rbac;

[TestFixture]
public class PermissionEvaluatorTests
{
    const string OperatorPolicyYaml = """
        schemaVersion: 1
        roles:
          sc-operator:
            bindings: [ "role:sc-operator" ]
            permissions:
              - "messages:view"
              - "messages:retry"
              - "messages:archive"
        """;

    const string ScopedPolicyYaml = """
        schemaVersion: 1
        roles:
          scoped-role:
            bindings: [ "group:/devops/sales" ]
            permissions:
              - permission: "messages:retry"
                scope: { allow: ["acme.sales.*"], deny: [] }
        """;

    const string AdminPolicyYaml = """
        schemaVersion: 1
        roles:
          sc-admin:
            bindings: [ "role:sc-admin" ]
            permissions: [ "*" ]
        """;

    [Test]
    public void Operator_has_retry_but_not_admin_only_permission()
    {
        var policy = RbacPolicyLoader.Parse(OperatorPolicyYaml);
        var evaluator = new PermissionEvaluator(() => policy);
        var user = PrincipalWithRoles("sc-operator");

        Assert.That(evaluator.HasPermission(user, "messages:retry"), Is.True);
        Assert.That(evaluator.HasPermission(user, "configuration:manage"), Is.False);
    }

    [Test]
    public void IsInScope_applies_allow_and_deny_patterns()
    {
        var evaluator = new PermissionEvaluator(() => RbacPolicyLoader.Parse(ScopedPolicyYaml));
        var user = PrincipalWithGroups("/devops/sales");

        Assert.That(evaluator.IsInScope(user, "messages:retry", "acme.sales.orders"), Is.True);
        Assert.That(evaluator.IsInScope(user, "messages:retry", "acme.finance.ap"), Is.False);
    }

    [Test]
    public void Wildcard_permission_grants_access_to_all_permissions()
    {
        var evaluator = new PermissionEvaluator(() => RbacPolicyLoader.Parse(AdminPolicyYaml));
        var user = PrincipalWithRoles("sc-admin");

        Assert.That(evaluator.HasPermission(user, "messages:retry"), Is.True);
        Assert.That(evaluator.HasPermission(user, "configuration:manage"), Is.True);
        Assert.That(evaluator.HasPermission(user, "anything:else"), Is.True);
    }

    [Test]
    public void Wildcard_permission_is_in_scope_for_any_resource()
    {
        var evaluator = new PermissionEvaluator(() => RbacPolicyLoader.Parse(AdminPolicyYaml));
        var user = PrincipalWithRoles("sc-admin");

        Assert.That(evaluator.IsInScope(user, "messages:retry", "any.queue"), Is.True);
        Assert.That(evaluator.IsInScope(user, "anything:else", "another.queue"), Is.True);
    }

    [Test]
    public void Unrestricted_grant_is_in_scope_for_any_resource()
    {
        var evaluator = new PermissionEvaluator(() => RbacPolicyLoader.Parse(OperatorPolicyYaml));
        var user = PrincipalWithRoles("sc-operator");

        // messages:retry is granted without a scope, so any resource is in scope
        Assert.That(evaluator.IsInScope(user, "messages:retry", "any.queue"), Is.True);
        Assert.That(evaluator.IsInScope(user, "messages:retry", "another.queue"), Is.True);
    }

    [Test]
    public void User_without_matching_role_has_no_permissions()
    {
        var evaluator = new PermissionEvaluator(() => RbacPolicyLoader.Parse(OperatorPolicyYaml));
        var user = PrincipalWithRoles("sc-viewer");

        Assert.That(evaluator.HasPermission(user, "messages:view"), Is.False);
    }

    [Test]
    public void Resolve_returns_effective_permissions_for_user()
    {
        var evaluator = new PermissionEvaluator(() => RbacPolicyLoader.Parse(OperatorPolicyYaml));
        var user = PrincipalWithRoles("sc-operator");

        var effective = evaluator.Resolve(user);

        var permissions = effective.Grants.Select(g => g.Permission).ToArray();
        Assert.That(permissions, Does.Contain("messages:retry"));
        Assert.That(permissions, Does.Contain("messages:view"));
        Assert.That(permissions, Does.Contain("messages:archive"));
    }

    [Test]
    public void IsInScope_grant_deny_in_one_role_does_not_block_allow_in_another_role()
    {
        // role-a covers messages:retry but explicitly denies the target resource
        // role-b covers messages:retry and allows the same resource
        // A user in both roles must be permitted — a deny in one grant must not
        // leak across to a separate grant from a different role.
        const string yaml = """
            schemaVersion: 1
            roles:
              role-a:
                bindings: [ "role:role-a" ]
                permissions:
                  - permission: "messages:retry"
                    scope: { allow: ["acme.*"], deny: ["acme.finance.*"] }
              role-b:
                bindings: [ "role:role-b" ]
                permissions:
                  - permission: "messages:retry"
                    scope: { allow: ["acme.finance.*"], deny: [] }
            """;

        var evaluator = new PermissionEvaluator(() => RbacPolicyLoader.Parse(yaml));

        // user is in both roles
        var identity = new ClaimsIdentity("Bearer");
        identity.AddClaim(new Claim("role", "role-a"));
        identity.AddClaim(new Claim("role", "role-b"));
        var user = new ClaimsPrincipal(identity);

        // role-a denies acme.finance.ap but role-b allows it — overall: permitted
        Assert.That(evaluator.IsInScope(user, "messages:retry", "acme.finance.ap"), Is.True,
            "Grant B's allow should win independently of grant A's deny");

        // role-a allows acme.sales.orders; role-b also allows it — still permitted
        Assert.That(evaluator.IsInScope(user, "messages:retry", "acme.sales.orders"), Is.True);
    }

    [Test]
    public void Resolve_deduplicates_identical_grants_from_multiple_roles()
    {
        // Both role-a and role-b grant messages:view with no scope.
        // A user in both roles should see exactly one messages:view entry in the descriptor.
        const string yaml = """
            schemaVersion: 1
            roles:
              role-a:
                bindings: [ "role:role-a" ]
                permissions: [ "messages:view" ]
              role-b:
                bindings: [ "role:role-b" ]
                permissions: [ "messages:view" ]
            """;

        var evaluator = new PermissionEvaluator(() => RbacPolicyLoader.Parse(yaml));

        var identity = new ClaimsIdentity("Bearer");
        identity.AddClaim(new Claim("role", "role-a"));
        identity.AddClaim(new Claim("role", "role-b"));
        var user = new ClaimsPrincipal(identity);

        var effective = evaluator.Resolve(user);

        var viewGrants = effective.Grants.Where(g => g.Permission == "messages:view").ToArray();
        Assert.That(viewGrants, Has.Length.EqualTo(1),
            "Two roles granting the same permission+scope must yield exactly one entry in the descriptor");
    }

    [Test]
    public void Resolve_preserves_distinct_scopes_for_same_permission()
    {
        // role-a grants messages:retry scoped to acme.sales.*, role-b grants it scoped to acme.finance.*.
        // Both entries must appear (OR semantics: user can retry in either scope).
        const string yaml = """
            schemaVersion: 1
            roles:
              role-a:
                bindings: [ "role:role-a" ]
                permissions:
                  - permission: "messages:retry"
                    scope: { allow: ["acme.sales.*"], deny: [] }
              role-b:
                bindings: [ "role:role-b" ]
                permissions:
                  - permission: "messages:retry"
                    scope: { allow: ["acme.finance.*"], deny: [] }
            """;

        var evaluator = new PermissionEvaluator(() => RbacPolicyLoader.Parse(yaml));

        var identity = new ClaimsIdentity("Bearer");
        identity.AddClaim(new Claim("role", "role-a"));
        identity.AddClaim(new Claim("role", "role-b"));
        var user = new ClaimsPrincipal(identity);

        var effective = evaluator.Resolve(user);

        var retryGrants = effective.Grants.Where(g => g.Permission == "messages:retry").ToArray();
        Assert.That(retryGrants, Has.Length.EqualTo(2),
            "Two roles granting the same permission with different scopes must yield two entries (OR semantics)");
    }

    static ClaimsPrincipal PrincipalWithRoles(params string[] roles)
    {
        var identity = new ClaimsIdentity("Bearer");
        foreach (var role in roles)
        {
            identity.AddClaim(new Claim("role", role));
        }
        return new ClaimsPrincipal(identity);
    }

    static ClaimsPrincipal PrincipalWithGroups(params string[] groups)
    {
        var identity = new ClaimsIdentity("Bearer");
        foreach (var group in groups)
        {
            identity.AddClaim(new Claim("group", group));
        }
        return new ClaimsPrincipal(identity);
    }
}
