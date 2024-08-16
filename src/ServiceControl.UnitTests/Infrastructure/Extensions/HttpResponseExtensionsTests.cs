namespace ServiceControl.UnitTests.Infrastructure.Extensions
{
    using System;
    using System.Linq;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.WebUtilities;
    using Microsoft.Extensions.Primitives;
    using NUnit.Framework;
    using Persistence.Infrastructure;
    using ServiceControl.Infrastructure.WebApi;

    [TestFixture]
    public class HttpResponseExtensionsTests
    {
        [Test]
        public void WithPagingLinks_ReturnsLinksWithRelativeUriButWithoutApiPrefix()
        {
            var pagingHeaders = GetLinks(totalResults: 200, currentPage: 3, path: "test1/test2");

            Assert.Contains("<test1/test2?page=4>; rel=\"next\"", pagingHeaders);
            Assert.Contains("<test1/test2?page=4>; rel=\"last\"", pagingHeaders);
            Assert.Contains("<test1/test2?page=2>; rel=\"prev\"", pagingHeaders);
            Assert.Contains("<test1/test2?page=1>; rel=\"first\"", pagingHeaders);
        }

        [Test]
        public void WithPagingLinks_KeepsExistingQueryParams()
        {
            var pagingHeaders = GetLinks(totalResults: 100, path: "test", queryParams: "token=abc&id=42");

            Assert.Contains("<test?token=abc&id=42&page=2>; rel=\"next\"", pagingHeaders);
            Assert.Contains("<test?token=abc&id=42&page=2>; rel=\"last\"", pagingHeaders);
        }

        [Test]
        public void WithPagingLinks_WhenHasNextPage_AddNextPageLink()
        {
            var pagingHeaders = GetLinks(totalResults: 51);

            Assert.Contains("<?page=2>; rel=\"next\"", pagingHeaders);
        }

        [Test]
        public void WithPagingLinks_WhenHasNoNextPage_AddNoNextPageLink()
        {
            var pagingHeaders = GetLinks(totalResults: 50);

            Assert.IsEmpty(pagingHeaders);
        }

        [Test]
        public void WithPagingLinks_WhenHasNextPage_AddLastPageLink()
        {
            var pagingHeaders = GetLinks(totalResults: 51, 150);

            Assert.Contains("<?page=3>; rel=\"last\"", pagingHeaders);
        }

        [Test]
        public void WithPagingLinks_WhenHasNoNextPage_AddNoLastPageLink()
        {
            var pagingHeaders = GetLinks(totalResults: 49, 150);

            Assert.IsEmpty(pagingHeaders);
        }

        [Test]
        public void WithPagingLinks_WhenHasPreviousPage_AddPreviousPageLink()
        {
            var pagingHeaders = GetLinks(totalResults: 120, currentPage: 3);

            Assert.Contains("<?page=2>; rel=\"prev\"", pagingHeaders);
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

            Assert.Contains("<?page=1>; rel=\"first\"", pagingHeaders);
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

            Assert.Contains("<?per_page=100&page=3>; rel=\"next\"", pagingHeaders);
            Assert.Contains("<?per_page=100&page=3>; rel=\"last\"", pagingHeaders);
            Assert.Contains("<?per_page=100&page=1>; rel=\"prev\"", pagingHeaders);
            Assert.Contains("<?per_page=100&page=1>; rel=\"first\"", pagingHeaders);
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