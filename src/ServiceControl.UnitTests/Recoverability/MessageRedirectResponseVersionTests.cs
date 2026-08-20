namespace ServiceControl.UnitTests.Operations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using NUnit.Framework;
    using ServiceControl.MessageRedirects.Api;
    using ServiceControl.Persistence.Infrastructure;
    using ServiceControl.Persistence.MessageRedirects;

    [TestFixture]
    public class MessageRedirectResponseVersionTests
    {
        [Test]
        public async Task Two_pages_of_redirects_do_not_share_a_version()
        {
            var store = Store("a@machine1", "c@machine3", "e@machine5");

            var firstPage = await Read(store, new PagingInfo(page: 1, pageSize: 2));
            var firstPageAgain = await Read(store, new PagingInfo(page: 1, pageSize: 2));
            var secondPage = await Read(store, new PagingInfo(page: 2, pageSize: 2));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(firstPage.Rows, Has.Count.EqualTo(2), "two redirects on the first page");
                Assert.That(secondPage.Rows, Has.Count.EqualTo(1), "and the third on the second, so the bodies differ");
                Assert.That(firstPage.Etag, Is.Not.Null.And.Not.Empty, "the first page sent no validator");
                Assert.That(firstPageAgain.Etag, Is.EqualTo(firstPage.Etag),
                    "the first page's validator moved between two reads of unchanged data, so this test cannot judge anything");
                Assert.That(secondPage.Etag, Is.Not.EqualTo(firstPage.Etag),
                    "a client following the Link rel=next header while revalidating would render page one as page two");
            }
        }

        [Test]
        public async Task Two_sort_orders_of_redirects_do_not_share_a_version()
        {
            var store = Store("a@machine1", "c@machine3", "e@machine5");

            var ascending = await Read(store, new PagingInfo(page: 1, pageSize: 2), sort: "from_physical_address", direction: "asc");
            var descending = await Read(store, new PagingInfo(page: 1, pageSize: 2), sort: "from_physical_address", direction: "desc");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(ascending.Rows.First().FromPhysicalAddress, Is.EqualTo("a@machine1"), "ascending starts at the first address");
                Assert.That(descending.Rows.First().FromPhysicalAddress, Is.EqualTo("e@machine5"), "descending starts at the last, so the bodies differ");
                Assert.That(ascending.Etag, Is.Not.Null.And.Not.Empty, "the ascending page sent no validator");
                Assert.That(descending.Etag, Is.Not.EqualTo(ascending.Etag),
                    "a client that switches sort order while holding a validator is told the reordered page is unchanged");
            }
        }

        static async Task<(string Etag, IList<MessageRedirectsController.RedirectsQueryResult> Rows)> Read(
            IMessageRedirectsDataStore store, PagingInfo pagingInfo, string sort = null, string direction = null)
        {
            var controller = new MessageRedirectsController(null, store, null)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };

            var rows = await controller.Redirects(sort, direction, pagingInfo);

            return (controller.Response.Headers.ETag.ToString(), rows.ToList());
        }

        static IMessageRedirectsDataStore Store(params string[] fromAddresses) =>
            new FakeStore([.. fromAddresses.Select((from, index) => new MessageRedirect
            {
                FromPhysicalAddress = from,
                ToPhysicalAddress = $"destination{index}@machine",
                LastModified = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc)
            })]);

        class FakeStore(IReadOnlyList<MessageRedirect> redirects) : IMessageRedirectsDataStore
        {
            public Task<IReadOnlyList<MessageRedirect>> GetRedirects(CancellationToken cancellationToken = default) =>
                Task.FromResult(redirects);

            public Task AddRedirect(MessageRedirect redirect, CancellationToken cancellationToken = default) => Task.CompletedTask;

            public Task UpdateRedirect(MessageRedirect redirect, CancellationToken cancellationToken = default) => Task.CompletedTask;

            public Task RemoveRedirect(MessageRedirect redirect, CancellationToken cancellationToken = default) => Task.CompletedTask;
        }
    }
}
