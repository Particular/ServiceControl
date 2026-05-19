namespace ServiceControl.UnitTests.Infrastructure.WebApi;

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Primitives;
using NUnit.Framework;
using ServiceControl.Audit.Infrastructure;
using ServiceControl.Audit.Infrastructure.WebApi;

[TestFixture]
public class SortInfoModelBinderTests
{
    [TestCase("processed_at")]
    [TestCase("critical_time")]
    [TestCase("message_type")]
    public async Task Preserves_valid_audit_sort_field(string sort)
    {
        var sortInfo = await Bind(sort, "asc");

        Assert.That(sortInfo.Sort, Is.EqualTo(sort));
    }

    [Test]
    public async Task Falls_back_to_time_sent_for_unknown_sort_field()
    {
        var sortInfo = await Bind("not_a_real_field", "asc");

        Assert.That(sortInfo.Sort, Is.EqualTo("time_sent"));
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
