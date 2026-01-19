namespace ServiceControl.AcceptanceTesting.Cors
{
    using System;

    /// <summary>
    /// Helper class to configure CORS environment variables for acceptance tests.
    /// Environment variables must be set before the ServiceControl instance starts.
    /// </summary>
    /// <remarks>
    /// Creates a new CORS test configuration.
    /// </remarks>
    /// <param name="instanceType">The instance type (determines environment variable prefix)</param>
    public class CorsTestConfiguration(ServiceControlInstanceType instanceType) : IDisposable
    {
        readonly string envVarPrefix = EnvironmentVariablePrefixes.GetPrefix(instanceType);
        bool disposed;

        /// <summary>
        /// Configures CORS to allow any origin (default behavior for backwards compatibility).
        /// </summary>
        public CorsTestConfiguration WithAllowAnyOrigin()
        {
            SetEnvironmentVariable("CORS_ALLOWANYORIGIN", "true");
            return this;
        }

        /// <summary>
        /// Configures CORS to disallow any origin (effectively disabling CORS).
        /// </summary>
        public CorsTestConfiguration WithDisallowAnyOrigin()
        {
            SetEnvironmentVariable("CORS_ALLOWANYORIGIN", "false");
            return this;
        }

        /// <summary>
        /// Configures CORS with specific allowed origins.
        /// Setting allowed origins automatically disables AllowAnyOrigin.
        /// </summary>
        /// <param name="origins">Comma-separated list of allowed origins (e.g., "https://app.example.com,https://admin.example.com")</param>
        public CorsTestConfiguration WithAllowedOrigins(string origins)
        {
            SetEnvironmentVariable("CORS_ALLOWEDORIGINS", origins);
            return this;
        }

        /// <summary>
        /// Configures CORS to be completely disabled (no origins allowed).
        /// </summary>
        public CorsTestConfiguration WithCorsDisabled()
        {
            SetEnvironmentVariable("CORS_ALLOWANYORIGIN", "false");
            // Don't set CORS_ALLOWEDORIGINS - leaves it empty
            return this;
        }

        /// <summary>
        /// Clears all CORS environment variables.
        /// Called automatically on Dispose.
        /// </summary>
        public void ClearConfiguration()
        {
            ClearEnvironmentVariable("CORS_ALLOWANYORIGIN");
            ClearEnvironmentVariable("CORS_ALLOWEDORIGINS");
        }

        void SetEnvironmentVariable(string name, string value) => Environment.SetEnvironmentVariable(envVarPrefix + name, value);

        void ClearEnvironmentVariable(string name) => Environment.SetEnvironmentVariable(envVarPrefix + name, null);

        public void Dispose()
        {
            if (!disposed)
            {
                ClearConfiguration();
                disposed = true;
            }
        }
    }
}
