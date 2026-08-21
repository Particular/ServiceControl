namespace ServiceControl.Persistence.Tests;

using NUnit.Framework;
using ServiceControl.Persistence.Infrastructure;

static class VersionAssert
{
    public static void Moved(DataVersion before, DataVersion after, string because)
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(before.HasValue, Is.True, "there was no version to move");
            Assert.That(after.HasValue, Is.True, "the version went missing rather than moving");
            Assert.That(after.Matches(before), Is.False, because);
        }
    }

    public static void Matches(DataVersion first, DataVersion second, string because)
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(first.HasValue, Is.True, "there was no version to hold");
            Assert.That(second.Matches(first), Is.True, because);
        }
    }

    public static bool Matches(this DataVersion one, DataVersion other) =>
        one.HasValue && other.HasValue && one.Equals(other);
}
