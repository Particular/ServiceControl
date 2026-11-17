namespace ServiceControl.UnitTests.Migration;

using System;
using NUnit.Framework;
using ServiceControl.Persistence;

[TestFixture]
class CustomCheckStatusTests
{
    // CustomCheckDetail carries the status as a bool, so a third member would be copied as Pass.
    [Test]
    public void The_status_enum_still_has_exactly_the_two_values_a_bool_can_carry() =>
        Assert.That(Enum.GetValues<Status>(), Is.EqualTo(new[] { Status.Fail, Status.Pass }));
}
