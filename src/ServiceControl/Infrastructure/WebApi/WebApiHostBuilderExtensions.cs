﻿namespace ServiceControl.Infrastructure.WebApi
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Formatters;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Net.Http.Headers;
    using ServiceControl.CompositeViews.Messages;

    static class WebApiHostBuilderExtensions
    {
        public static void AddWebApi(this WebApplicationBuilder hostBuilder, List<Assembly> apiAssemblies, string rootUrl)
        {
            hostBuilder.WebHost.UseUrls(rootUrl);

            foreach (var apiAssembly in apiAssemblies)
            {
                hostBuilder.Services.RegisterApiTypes(apiAssembly);
                // TODO Why are we registering all concrete types? This blows up the ASP.Net Core DI container
                hostBuilder.Services.RegisterConcreteTypes(apiAssembly);
            }

            hostBuilder.Services.AddCors(options => options.AddDefaultPolicy(Cors.GetDefaultPolicy()));

            // We're not explicitly adding Gzip here because it's already in the default list of supported compressors
            hostBuilder.Services.AddResponseCompression();
            hostBuilder.Services.AddControllers(options =>
            {
                options.Filters.Add<XParticularVersionHttpHandler>();
                options.Filters.Add<CachingHttpHandler>();
                options.Filters.Add<NotModifiedStatusHttpHandler>();

                options.OutputFormatters.Clear();
                // TODO Revisit to see if we can switch to System.Text.Json
                var formatter = new NewtonsoftJsonOutputFormatter(JsonNetSerializerSettings.CreateDefault(),
                    ArrayPool<char>.Shared, options, new MvcNewtonsoftJsonOptions())
                {
                    SupportedMediaTypes = { new MediaTypeHeaderValue("application/vnd.particular.1+json") }
                };
                options.OutputFormatters.Add(formatter);

                options.ModelBinderProviders.Insert(0, new PagingInfoModelBindingProvider());
                options.ModelBinderProviders.Insert(0, new SortInfoModelBindingProvider());
            });
            hostBuilder.Services.AddSignalR();
        }

        static void RegisterApiTypes(this IServiceCollection serviceCollection, Assembly assembly)
        {
            var apiTypes = assembly.DefinedTypes
                .Where(ti => ti.IsClass &&
                             !ti.IsAbstract &&
                             !ti.IsGenericTypeDefinition &&
                             ti.GetInterfaces().Any(t => t == typeof(IApi)))
                .Select(ti => ti.AsType());


            foreach (var apiType in apiTypes)
            {
                if (!serviceCollection.Any(sd => sd.ServiceType == apiType))
                {
                    serviceCollection.AddSingleton(apiType);
                }
            }
        }

        static void RegisterConcreteTypes(this IServiceCollection serviceCollection, Assembly assembly)
        {
            var concreteTypes = assembly.DefinedTypes
                .Where(ti => ti.IsClass &&
                             !ti.IsAbstract &&
                             !ti.IsSubclassOf(typeof(Delegate)) &&
                             !ti.IsGenericTypeDefinition &&
                             !ti.GetInterfaces().Any())
                .Select(ti => ti.AsType());

            foreach (var concreteType in concreteTypes)
            {
                if (!serviceCollection.Any(sd => sd.ServiceType == concreteType))
                {
                    serviceCollection.AddSingleton(concreteType);
                }
            }
        }
    }
}