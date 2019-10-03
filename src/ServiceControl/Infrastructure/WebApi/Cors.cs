namespace ServiceControl.Infrastructure.WebApi
{
    using System.Threading.Tasks;
    using System.Web.Cors;
    using Microsoft.Owin.Cors;
    using Raven.Abstractions.Extensions;

    class Cors
    {
        static Task<CorsPolicy> CachedDefaultPolicy
        {
            get
            {
                if (defaultPolicy == null)
                {
                    defaultPolicy = Task.FromResult(GetDefaultPolicy());
                }

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

            policy.ExposedHeaders.AddRange(new[] {"ETag", "Last-Modified", "Link", "Total-Count", "X-Particular-Version"});
            policy.Headers.AddRange(new[] {"Origin", "X-Requested-With", "Content-Type", "Accept"});
            policy.Methods.AddRange(new[] {"POST", "GET", "PUT", "DELETE", "OPTIONS", "PATCH"});

            return policy;
        }

        public static CorsOptions AuditCorsOptions = GetCorsOptions();
        static Task<CorsPolicy> defaultPolicy;
    }
}