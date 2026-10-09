namespace ServiceControl.UnitTests.Migration;

using System;
using NUnit.Framework;
using ServiceControl.Migration.Checks;

[TestFixture]
class RetryHistoryDepthIsSafeCheckTests
{
    [Test]
    public void Passes_at_the_default_depth() =>
        Assert.DoesNotThrowAsync(() => new RetryHistoryDepthIsSafeCheck(10).Run());

    [TestCase(0)]
    [TestCase(-1)]
    public void Refuses_a_depth_that_empties_the_table(int depth)
    {
        var exception = Assert.ThrowsAsync<Exception>(() => new RetryHistoryDepthIsSafeCheck(depth).Run());

        Assert.That(exception.Message, Does.Contain("RetryHistoryDepth").And.Contain("HistoricRetryOperations"));
    }
}
