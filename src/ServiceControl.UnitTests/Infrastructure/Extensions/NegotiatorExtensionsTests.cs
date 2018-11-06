namespace ServiceControl.UnitTests.Infrastructure.Extensions
{
    using System;
    using System.Linq;
    using Nancy;
    using Nancy.Responses.Negotiation;
    using NUnit.Framework;
    using ServiceBus.Management.Infrastructure.Extensions;

    [TestFixture]
    public class NegotiatorExtensionsTests
    {
        [Test]
        public void WithPagingLinks_ReturnsLinksWithRelativeUri()
        {
            var pagingHeaders = GetLinks(totalResults: 200, currentPage: 3, path: "/api/test");

            Assert.Contains("</api/test?page=4>; rel=\"next\"", pagingHeaders);
            Assert.Contains("</api/test?page=4>; rel=\"last\"", pagingHeaders);
            Assert.Contains("</api/test?page=2>; rel=\"prev\"", pagingHeaders);
            Assert.Contains("</api/test?page=1>; rel=\"first\"", pagingHeaders);
        }

        [Test]
        public void WithPagingLinks_KeepsExistingQueryParams()
        {
            var pagingHeaders = GetLinks(totalResults: 100, path: "/api", queryParams: "token=abc&id=42");

            Assert.Contains("</api?token=abc&id=42&page=2>; rel=\"next\"", pagingHeaders);
            Assert.Contains("</api?token=abc&id=42&page=2>; rel=\"last\"", pagingHeaders);
        }

        [Test]
        public void WithPagingLinks_WhenHasNextPage_AddNextPageLink()
        {
            var pagingHeaders = GetLinks(totalResults: 51);

            Assert.Contains("</?page=2>; rel=\"next\"", pagingHeaders);
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

            Assert.Contains("</?page=3>; rel=\"last\"", pagingHeaders);
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

            Assert.Contains("</?page=2>; rel=\"prev\"", pagingHeaders);
        }

        [Test]
        public void WithPagingLinks_WhenHasNoPreviousPage_AddNoPreviousPageLink()
        {
            var pagingHeaders = GetLinks(totalResults: 51, currentPage: 1);

            Assert.IsFalse(pagingHeaders.Any(link => link.Contains("rel=\"prev\"")));
        }

        [Test]
        public void WithPagingLinks_WhenHasPreviousPage_AddFirstPageLink()
        {
            var pagingHeaders = GetLinks(totalResults: 200, currentPage: 4);

            Assert.Contains("</?page=1>; rel=\"first\"", pagingHeaders);
        }

        [Test]
        public void WithPagingLinks_WhenHasNoPreviousPage_AddNoFirstPageLink()
        {
            var pagingHeaders = GetLinks(totalResults: 200, currentPage: 1);

            Assert.IsFalse(pagingHeaders.Any(link => link.Contains("rel=\"first\"")));
        }

        [Test]
        public void WithPagingLinks_WhenDefiningCustomPageSize_AdjustPagingToCustomPageSize()
        {
            var pagingHeaders = GetLinks(totalResults: 300, currentPage: 2, resultsPerPage: 100);

            Assert.Contains("</?per_page=100&page=3>; rel=\"next\"", pagingHeaders);
            Assert.Contains("</?per_page=100&page=3>; rel=\"last\"", pagingHeaders);
            Assert.Contains("</?per_page=100&page=1>; rel=\"prev\"", pagingHeaders);
            Assert.Contains("</?per_page=100&page=1>; rel=\"first\"", pagingHeaders);
        }

        static string[] GetLinks(
            int totalResults, 
            int? highestTotalCountOfAllInstances = null, 
            int? currentPage = null, 
            int? resultsPerPage = null,
            string path = null,
            string queryParams = null)
        {
            var negotiator = new Negotiator(new NancyContext());
            var request = new Request("GET", $"name.tld:99{ path ?? string.Empty }", "schema");
            var queryString = "?";

            if (resultsPerPage.HasValue)
            {
                queryString += $"per_page={resultsPerPage.Value}&";
                request.Query.per_page = resultsPerPage.Value;
            }
            
            if (currentPage.HasValue)
            {
                queryString += $"page={currentPage.Value}&";
                request.Query.page = currentPage.Value;
            }

            queryString += queryParams;
            request.Url.Query = queryString.TrimEnd('&');

            negotiator.WithPagingLinks(totalResults, highestTotalCountOfAllInstances ?? totalResults, request);

            if (negotiator.NegotiationContext.Headers.TryGetValue("Link", out var links))
            {
                return links.Split(new[] {", "}, StringSplitOptions.RemoveEmptyEntries);
            }

            return new string[0];
        }
    }
}