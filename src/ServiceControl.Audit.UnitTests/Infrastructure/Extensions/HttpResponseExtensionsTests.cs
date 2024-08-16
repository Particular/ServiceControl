namespace ServiceControl.Audit.UnitTests.Infrastructure.Extensions
{
    using System;
    using System.Linq;
    using Audit.Infrastructure;
    using Audit.Infrastructure.WebApi;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.WebUtilities;
    using Microsoft.Extensions.Primitives;
    using NUnit.Framework;

    [TestFixture]
    public class HttpResponseExtensionsTests
    {
        [Test]
        public void WithPagingLinks_ReturnsLinksWithRelativeUriButWithoutApiPrefix()
        {
            var pagingHeaders = GetLinks(totalResults: 200, currentPage: 3, path: "test1/test2");

            Assert.That(pagingHeaders, Does.Contain("<test1/test2?page=4>; rel=\"next\""));
            Assert.That(pagingHeaders, Does.Contain("<test1/test2?page=4>; rel=\"last\""));
            Assert.That(pagingHeaders, Does.Contain("<test1/test2?page=2>; rel=\"prev\""));
            Assert.That(pagingHeaders, Does.Contain("<test1/test2?page=1>; rel=\"first\""));
        }

        [Test]
        public void WithPagingLinks_KeepsExistingQueryParams()
        {
            var pagingHeaders = GetLinks(totalResults: 100, path: "test", queryParams: "token=abc&id=42");

            Assert.That(pagingHeaders, Does.Contain("<test?token=abc&id=42&page=2>; rel=\"next\""));
            Assert.That(pagingHeaders, Does.Contain("<test?token=abc&id=42&page=2>; rel=\"last\""));
        }

        [Test]
        public void WithPagingLinks_WhenHasNextPage_AddNextPageLink()
        {
            var pagingHeaders = GetLinks(totalResults: 51);

            Assert.That(pagingHeaders, Does.Contain("<?page=2>; rel=\"next\""));
        }

        [Test]
        public void WithPagingLinks_WhenHasNoNextPage_AddNoNextPageLink()
        {
            var pagingHeaders = GetLinks(totalResults: 50);

            Assert.That(pagingHeaders, Is.Empty);
        }

        [Test]
        public void WithPagingLinks_WhenHasNextPage_AddLastPageLink()
        {
            var pagingHeaders = GetLinks(totalResults: 51, 150);

            Assert.That(pagingHeaders, Does.Contain("<?page=3>; rel=\"last\""));
        }

        [Test]
        public void WithPagingLinks_WhenHasNoNextPage_AddNoLastPageLink()
        {
            var pagingHeaders = GetLinks(totalResults: 49, 150);

            Assert.That(pagingHeaders, Is.Empty);
        }

        [Test]
        public void WithPagingLinks_WhenHasPreviousPage_AddPreviousPageLink()
        {
            var pagingHeaders = GetLinks(totalResults: 120, currentPage: 3);

            Assert.That(pagingHeaders, Does.Contain("<?page=2>; rel=\"prev\""));
        }

        [Test]
        public void WithPagingLinks_WhenHasNoPreviousPage_AddNoPreviousPageLink()
        {
            var pagingHeaders = GetLinks(totalResults: 51, currentPage: 1);

            Assert.That(pagingHeaders.Any(link => link.Contains("rel=\"prev\"")), Is.False);
        }

        [Test]
        public void WithPagingLinks_WhenHasPreviousPage_AddFirstPageLink()
        {
            var pagingHeaders = GetLinks(totalResults: 200, currentPage: 4);

            Assert.That(pagingHeaders, Does.Contain("<?page=1>; rel=\"first\""));
        }

        [Test]
        public void WithPagingLinks_WhenHasNoPreviousPage_AddNoFirstPageLink()
        {
            var pagingHeaders = GetLinks(totalResults: 200, currentPage: 1);

            Assert.That(pagingHeaders.Any(link => link.Contains("rel=\"first\"")), Is.False);
        }

        [Test]
        public void WithPagingLinks_WhenDefiningCustomPageSize_AdjustPagingToCustomPageSize()
        {
            var pagingHeaders = GetLinks(totalResults: 300, currentPage: 2, resultsPerPage: 100);

            Assert.That(pagingHeaders, Does.Contain("<?per_page=100&page=3>; rel=\"next\""));
            Assert.That(pagingHeaders, Does.Contain("<?per_page=100&page=3>; rel=\"last\""));
            Assert.That(pagingHeaders, Does.Contain("<?per_page=100&page=1>; rel=\"prev\""));
            Assert.That(pagingHeaders, Does.Contain("<?per_page=100&page=1>; rel=\"first\""));
        }

        static string[] GetLinks(
            int totalResults,
            int? highestTotalCountOfAllInstances = null,
            int? currentPage = null,
            int? resultsPerPage = null,
            string path = null,
            string queryParams = null)
        {
            var queryParameters = QueryHelpers.ParseQuery(queryParams);

            if (resultsPerPage.HasValue)
            {
                queryParameters["per_page"] = new StringValues(resultsPerPage.Value.ToString());
            }

            if (currentPage.HasValue)
            {
                queryParameters["page"] = new StringValues(currentPage.Value.ToString());
            }

            var httpContext = new DefaultHttpContext
            {
                Request =
                {
                    Method = "GET",
                    Path = $"/api/{path ?? string.Empty}",
                    Query = new QueryCollection(queryParameters)
                }
            };

            httpContext.Response.WithPagingLinks(new PagingInfo(currentPage, resultsPerPage), highestTotalCountOfAllInstances ?? totalResults, totalResults);

            return httpContext.Response.Headers.TryGetValue("Link", out var links) ? links.ToArray() : Array.Empty<string>();
        }
    }
}