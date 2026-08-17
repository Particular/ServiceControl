namespace ServiceControl.Persistence.Tests;

using System;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Persistence.MessageRedirects;

class MessageRedirectsDataStoreTests : PersistenceTestBase
{
    static readonly DateTime Noon = new(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task Returns_no_redirects_when_none_are_stored()
    {
        var redirects = await MessageRedirectsDataStore.GetRedirects();

        Assert.That(redirects, Is.Empty);
    }

    [Test]
    public async Task Stores_a_redirect()
    {
        await Add("Sales", "Sales.New");

        var redirects = await MessageRedirectsDataStore.GetRedirects();

        var redirect = redirects.FindByAddress("Sales");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(redirect, Is.Not.Null);
            Assert.That(redirect.ToPhysicalAddress, Is.EqualTo("Sales.New"));
            Assert.That(redirect.LastModified, Is.EqualTo(Noon));
            Assert.That(redirects.FindById(redirect.MessageRedirectId), Is.SameAs(redirect));
        }
    }

    [Test]
    public async Task Stores_several_redirects()
    {
        await Add("Sales", "Sales.New");
        await Add("Shipping", "Shipping.New");

        var redirects = await MessageRedirectsDataStore.GetRedirects();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(redirects, Has.Count.EqualTo(2));
            Assert.That(redirects.FindByAddress("Sales").ToPhysicalAddress, Is.EqualTo("Sales.New"));
            Assert.That(redirects.FindByAddress("Shipping").ToPhysicalAddress, Is.EqualTo("Shipping.New"));
        }
    }

    [Test]
    public async Task Updates_the_target_of_a_redirect()
    {
        await Add("Sales", "Sales.New");

        await MessageRedirectsDataStore.UpdateRedirect(new MessageRedirect
        {
            FromPhysicalAddress = "Sales",
            ToPhysicalAddress = "Sales.Newer",
            LastModified = Noon.AddHours(1)
        });

        var redirect = (await MessageRedirectsDataStore.GetRedirects()).FindByAddress("Sales");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(redirect.ToPhysicalAddress, Is.EqualTo("Sales.Newer"));
            Assert.That(redirect.LastModified, Is.EqualTo(Noon.AddHours(1)));
        }
    }

    [Test]
    public async Task Leaves_other_redirects_alone_when_one_is_updated()
    {
        await Add("Sales", "Sales.New");
        await Add("Shipping", "Shipping.New");

        await MessageRedirectsDataStore.UpdateRedirect(new MessageRedirect
        {
            FromPhysicalAddress = "Sales",
            ToPhysicalAddress = "Sales.Newer",
            LastModified = Noon.AddHours(1)
        });

        var redirects = await MessageRedirectsDataStore.GetRedirects();

        Assert.That(redirects.FindByAddress("Shipping").ToPhysicalAddress, Is.EqualTo("Shipping.New"));
    }

    [Test]
    public async Task Removes_a_redirect()
    {
        await Add("Sales", "Sales.New");
        await Add("Shipping", "Shipping.New");

        await MessageRedirectsDataStore.RemoveRedirect(new MessageRedirect { FromPhysicalAddress = "Sales" });

        var redirects = await MessageRedirectsDataStore.GetRedirects();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(redirects.FindByAddress("Sales"), Is.Null);
            Assert.That(redirects.FindByAddress("Shipping"), Is.Not.Null);
        }
    }

    [Test]
    public async Task Ignores_removing_a_redirect_that_is_not_there()
    {
        await Add("Sales", "Sales.New");

        await MessageRedirectsDataStore.RemoveRedirect(new MessageRedirect { FromPhysicalAddress = "Unknown" });

        Assert.That(await MessageRedirectsDataStore.GetRedirects(), Has.Count.EqualTo(1));
    }

    Task Add(string from, string to) =>
        MessageRedirectsDataStore.AddRedirect(new MessageRedirect
        {
            FromPhysicalAddress = from,
            ToPhysicalAddress = to,
            LastModified = Noon
        });
}
