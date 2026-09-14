namespace ServiceControl.Persistence.Tests;

using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.Persistence.EFCore.Infrastructure;

class RetentionLockTests : PersistenceTestBase
{
    [Test]
    public async Task A_second_acquisition_fails_while_the_first_is_held_and_succeeds_once_released()
    {
        var retentionLock = ServiceProvider.GetRequiredService<IRetentionLock>();

        var first = await retentionLock.TryAcquire();

        Assert.That(first, Is.Not.Null);
        Assert.That(await retentionLock.TryAcquire(), Is.Null, "the lock is held");

        await first.DisposeAsync();

        var second = await retentionLock.TryAcquire();

        Assert.That(second, Is.Not.Null, "the lock was released");

        await second.DisposeAsync();
    }
}
