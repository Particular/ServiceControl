namespace ServiceControl.Audit.Infrastructure.WebApi;

using System;
using Microsoft.AspNetCore.Mvc.ModelBinding;

public class PagingInfoModelBindingProvider : IModelBinderProvider
{
    public IModelBinder GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Metadata.ModelType == typeof(PagingInfo) ? new PagingInfoModelBinder() : null;
    }
}