namespace ServiceControl.Audit.Infrastructure.WebApi;

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ModelBinding;

public class SortInfoModelBinder : IModelBinder
{
#pragma warning disable PS0018 // IModelBinder declares this without a CancellationToken
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        bindingContext.HttpContext.Request.Query.TryGetValue("direction", out var directionValue);
        var direction = directionValue.ToString();
        if (direction is not "asc" and not "desc")
        {
            direction = "desc";
        }

        bindingContext.HttpContext.Request.Query.TryGetValue("sort", out var sortValue);
        var sort = sortValue.ToString();
        if (!SortInfo.AllowedSortOptions.Contains(sort))
        {
            sort = "time_sent";
        }

        bindingContext.Result = ModelBindingResult.Success(new SortInfo(sort, direction));
        return Task.CompletedTask;
    }
#pragma warning restore PS0018
}
