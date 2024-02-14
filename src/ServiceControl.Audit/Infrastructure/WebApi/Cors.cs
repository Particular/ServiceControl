﻿namespace ServiceControl.Audit.Infrastructure.WebApi
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Web.Cors;
    using Microsoft.Owin.Cors;

    class Cors
    {
        static Task<CorsPolicy> CachedDefaultPolicy
        {
            get
            {
                defaultPolicy ??= Task.FromResult(GetDefaultPolicy());

                return defaultPolicy;
            }
        }

        static CorsOptions GetCorsOptions()
        {
            return new CorsOptions
            {
                PolicyProvider = new CorsPolicyProvider
                {
                    PolicyResolver = context => CachedDefaultPolicy
                }
            };
        }

        static CorsPolicy GetDefaultPolicy()
        {
            var policy = new CorsPolicy
            {
                AllowAnyHeader = false,
                AllowAnyMethod = false,
                AllowAnyOrigin = true
            };

            AddRange(policy.ExposedHeaders, "ETag", "Last-Modified", "Link", "Total-Count", "X-Particular-Version");
            AddRange(policy.Headers, "Origin", "X-Requested-With", "Content-Type", "Accept");
            AddRange(policy.Methods, "POST", "GET", "PUT", "DELETE", "OPTIONS", "PATCH");

            return policy;
        }

        static void AddRange(IList<string> list, params string[] items)
        {
            foreach (var item in items)
            {
                list.Add(item);
            }
        }

        public static CorsOptions AuditCorsOptions = GetCorsOptions();
        static Task<CorsPolicy> defaultPolicy;
    }
}