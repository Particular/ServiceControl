namespace ServiceControl.Audit.Infrastructure.WebApi;

using System;
using System.Threading.Tasks;
using Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;

public class PagingInfoModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        bindingContext.HttpContext.Request.Query.TryGetValue("per_page", out var perPageValue);
        var maxResultsPerPage = Convert.ToInt32(perPageValue);
        if (maxResultsPerPage < 1)
        {
            maxResultsPerPage = PagingInfo.DefaultPageSize;
        }

        bindingContext.HttpContext.Request.Query.TryGetValue("page", out var pageValue);
        var page = Convert.ToInt32(pageValue);
        if (page < 1)
        {
            page = 1;
        }

        bindingContext.Result = ModelBindingResult.Success(new PagingInfo(page, maxResultsPerPage));
        return Task.CompletedTask;
    }
}