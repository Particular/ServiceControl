namespace ServiceControl.Infrastructure.Tests.Auth.Rbac;

using NUnit.Framework;
using ServiceControl.Infrastructure.Auth.Rbac;

/// <summary>
/// Unit tests for the queue-scope filtering logic documented in FilterByQueueScope.
/// These tests use ResourceScope.Permits (the in-process evaluator) to verify
/// the logical behaviour of scope patterns, since RavenDB's IAsyncDocumentQuery
/// is not easily mockable. The corresponding correctness of the RavenDB query
/// translation is verified by integration/acceptance tests.
///
/// Bug coverage:
/// (a) D3(a): Deny prefix patterns (e.g. 'Finance.*') must exclude all queues starting
///     with 'Finance.' — not just the literal string 'Finance'.
/// (b) D3(b): Allow and deny patterns must be matched case-insensitively against the
///     lower-cased queue address stored in the index.
/// </summary>
[TestFixture]
public class FilterByQueueScopeTests
{
    [Test]
    public void Deny_prefix_pattern_excludes_matching_queue()
    {
        // D3(a): "Sales.secret.*" should deny "sales.secret.payroll"
        var scope = new ResourceScope(
            allow: ["*"],
            deny: ["Sales.secret.*"]);

        Assert.That(scope.Permits("sales.secret.payroll"), Is.False,
            "Deny prefix 'Sales.secret.*' must exclude 'sales.secret.payroll'");
    }

    [Test]
    public void Deny_prefix_pattern_does_not_exclude_non_matching_queue()
    {
        // "Sales.secret.*" must NOT deny "sales.public.orders"
        var scope = new ResourceScope(
            allow: ["*"],
            deny: ["Sales.secret.*"]);

        Assert.That(scope.Permits("sales.public.orders"), Is.True,
            "Deny prefix 'Sales.secret.*' must NOT exclude 'sales.public.orders'");
    }

    [Test]
    public void Deny_exact_pattern_denies_only_exact_match()
    {
        // D3(a): exact deny "Finance" must NOT deny "finance.payroll".
        // Queue addresses are stored in lowercase in the index; the pattern is also
        // lowercased before comparison, so "Finance" → "finance".
        var scope = new ResourceScope(
            allow: ["*"],
            deny: ["Finance"]);

        Assert.That(scope.Permits("finance.payroll"), Is.True,
            "Exact deny 'Finance' (→ 'finance') must not deny 'finance.payroll'");
        Assert.That(scope.Permits("finance"), Is.False,
            "Exact deny 'Finance' (→ 'finance') must deny exact lowercase 'finance'");
    }

    [Test]
    public void Allow_prefix_pattern_lowercased_matches_stored_lower_case_queue()
    {
        // D3(b): FilterByQueueScope lowercases patterns before calling WhereStartsWith,
        // so "Sales.*" → prefix "sales." which matches stored "sales.orders".
        // We simulate this by pre-lowercasing the pattern, as the extension method does.
        var scope = new ResourceScope(
            allow: ["sales.*"],  // pre-lowercased, as the query method applies
            deny: []);

        Assert.That(scope.Permits("sales.orders"), Is.True,
            "Lowercased allow 'sales.*' must match stored lowercase address 'sales.orders'");
        Assert.That(scope.Permits("finance.ap"), Is.False,
            "Lowercased allow 'sales.*' must not match 'finance.ap'");
    }

    [Test]
    public void Mixed_case_allow_pattern_after_lowercasing_matches_stored_queue()
    {
        // D3(b): mixed-case allow "Sales.*" must match lowercase "sales.orders"
        // The extension method calls pattern.ToLowerInvariant() → "sales.*" → prefix "sales."
        var mixedCasePattern = "Sales.*";
        var lower = mixedCasePattern.ToLowerInvariant(); // "sales.*"
        var scope = new ResourceScope(allow: [lower], deny: []);

        Assert.That(scope.Permits("sales.orders"), Is.True,
            "Pattern 'Sales.*' lowercased to 'sales.*' must match 'sales.orders'");
    }

    [Test]
    public void Deny_prefix_pattern_wins_over_allow_wildcard()
    {
        // Full scenario: wildcard allow but Finance.* deny
        var scope = new ResourceScope(
            allow: ["*"],
            deny: ["Finance.*"]);

        Assert.That(scope.Permits("finance.accounts"), Is.False,
            "Finance.* deny wins over wildcard allow for 'finance.accounts'");
        Assert.That(scope.Permits("sales.orders"), Is.True,
            "Finance.* deny must not affect 'sales.orders'");
    }
}
