namespace ServiceControl.Persistence.Tests;

using NUnit.Framework;
using ServiceControl.Persistence.Infrastructure;

static class VersionAssert
{
    /// <summary>
    /// The data moved, so the version had to move with it. Checks the earlier version exists first,
    /// because <see cref="DataVersion.Matches"/> is false whenever either side is absent, so a store
    /// that stopped producing a version at all would otherwise satisfy the same assertion.
    /// </summary>
    public static void Moved(DataVersion before, DataVersion after, string because)
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(before.HasValue, Is.True, "there was no version to move");
            Assert.That(after.HasValue, Is.True, "the version went missing rather than moving");
            Assert.That(after.Matches(before), Is.False, because);
        }
    }

    /// <summary>Nothing changed, so a caller holding the earlier version still holds the current one.</summary>
    public static void Held(DataVersion first, DataVersion second, string because)
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(first.HasValue, Is.True, "there was no version to hold");
            Assert.That(second.Matches(first), Is.True, because);
        }
    }
}
