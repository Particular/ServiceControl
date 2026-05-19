namespace ServiceControl.UnitTests.Infrastructure.WebApi
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.ModelBinding;
    using Microsoft.Extensions.Primitives;
    using NUnit.Framework;
    using Persistence.Infrastructure;
    using ServiceControl.Infrastructure.WebApi;

    [TestFixture]
    public class SortInfoModelBinderTests
    {
        // Failed-message endpoints (/api/errors) support these sort fields in the
        // RavenDB layer. The model binder must not silently rewrite them away.
        [TestCase("time_of_failure")]
        [TestCase("modified")]
        public async Task Preserves_valid_failed_message_sort_field(string sort)
        {
            var sortInfo = await Bind(sort, "asc");

            Assert.That(sortInfo.Sort, Is.EqualTo(sort));
        }

        static async Task<SortInfo> Bind(string sort, string direction)
        {
            var httpContext = new DefaultHttpContext
            {
                Request =
                {
                    Query = new QueryCollection(new Dictionary<string, StringValues>
                    {
                        ["sort"] = sort,
                        ["direction"] = direction,
                    })
                }
            };

            var bindingContext = new DefaultModelBindingContext
            {
                ActionContext = new ActionContext { HttpContext = httpContext },
                ModelName = "sortInfo",
            };

            await new SortInfoModelBinder().BindModelAsync(bindingContext);

            return (SortInfo)bindingContext.Result.Model;
        }
    }
}
