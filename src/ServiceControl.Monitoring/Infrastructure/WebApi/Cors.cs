namespace ServiceControl.Monitoring.Infrastructure.WebApi
{
    using System.Threading.Tasks;
    using System.Web.Cors;
    using Extensions;
    using Microsoft.Owin.Cors;

    public class Cors
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

        internal static CorsOptions GetDefaultCorsOptions()
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

            policy.ExposedHeaders.AddRange(new[] { "ETag", "Last-Modified", "Link", "Total-Count", "X-Particular-Version" });
            policy.Headers.AddRange(new[] { "Origin", "X-Requested-With", "Content-Type", "Accept" });
            policy.Methods.AddRange(new[] { "POST", "GET", "PUT", "DELETE", "OPTIONS", "PATCH" });

            return policy;
        }

        public static CorsOptions MonitoringCorsOptions = GetDefaultCorsOptions();
        static Task<CorsPolicy> defaultPolicy;
    }
}