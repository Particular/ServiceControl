namespace ServiceControl.Infrastructure.WebApi;

using System;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Persistence.Infrastructure;

public class SortInfoModelBindingProvider : IModelBinderProvider
{
    public IModelBinder GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Metadata.ModelType == typeof(SortInfo) ? new SortInfoModelBinder() : null;
    }
}