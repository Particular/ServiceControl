namespace ServiceControl.Infrastructure.Tests.Auth.Rbac;

using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ServiceControl.Infrastructure.Auth.Rbac;

[TestFixture]
public class PermissionsTests
{
    [Test]
    public void All_contains_messages_retry()
        => Assert.That(Permissions.All, Does.Contain(Permissions.MessagesRetry));

    [Test]
    public void All_entries_are_unique()
        => Assert.That(Permissions.All.Distinct().Count(), Is.EqualTo(Permissions.All.Count));

    [Test]
    public void All_entries_match_resource_action_pattern_or_are_wildcard()
    {
        var pattern = new Regex(@"^[a-z]+:[a-z-]+$");
        foreach (var permission in Permissions.All)
        {
            Assert.That(
                permission == "*" || pattern.IsMatch(permission),
                Is.True,
                $"Permission '{permission}' does not match 'resource:action' pattern");
        }
    }
}
