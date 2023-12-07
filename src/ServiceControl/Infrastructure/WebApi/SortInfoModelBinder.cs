namespace ServiceControl.Infrastructure.WebApi;

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Persistence.Infrastructure;

public class SortInfoModelBinder : IModelBinder
{
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
        if (!AllowableSortOptions.Contains(sort))
        {
            sort = "time_sent";
        }

        bindingContext.Result = ModelBindingResult.Success(new SortInfo(sort, direction));
        return Task.CompletedTask;
    }

    static readonly FrozenSet<string> AllowableSortOptions = new HashSet<string>
    {
        "processed_at",
        "id",
        "message_type",
        "time_sent",
        "critical_time",
        "delivery_time",
        "processing_time",
        "status",
        "message_id"
    }.ToFrozenSet();
}