namespace ServiceControl.AcceptanceTesting.Https
{
    using System;

    /// <summary>
    /// Helper class to configure HTTPS environment variables for acceptance tests.
    /// Environment variables must be set before the ServiceControl instance starts.
    /// </summary>
    /// <remarks>
    /// Creates a new HTTPS test configuration.
    /// </remarks>
    /// <param name="instanceType">The instance type (determines environment variable prefix)</param>
    public class HttpsTestConfiguration(ServiceControlInstanceType instanceType) : IDisposable
    {
        readonly string envVarPrefix = EnvironmentVariablePrefixes.GetPrefix(instanceType);
        bool disposed;

        /// <summary>
        /// Configures HTTPS redirect to be enabled.
        /// When enabled, HTTP requests will be redirected to HTTPS.
        /// </summary>
        public HttpsTestConfiguration WithRedirectHttpToHttps()
        {
            SetEnvironmentVariable("HTTPS_REDIRECTHTTPTOHTTPS", "true");
            return this;
        }

        /// <summary>
        /// Configures HTTPS redirect to be disabled (default behavior).
        /// </summary>
        public HttpsTestConfiguration WithRedirectHttpToHttpsDisabled()
        {
            SetEnvironmentVariable("HTTPS_REDIRECTHTTPTOHTTPS", "false");
            return this;
        }

        /// <summary>
        /// Configures the port to redirect HTTPS requests to.
        /// Only used when RedirectHttpToHttps is true.
        /// </summary>
        /// <param name="port">The HTTPS port to redirect to</param>
        public HttpsTestConfiguration WithHttpsPort(int port)
        {
            SetEnvironmentVariable("HTTPS_PORT", port.ToString());
            return this;
        }

        /// <summary>
        /// Configures HSTS (HTTP Strict Transport Security) to be enabled.
        /// HSTS instructs browsers to only access the site via HTTPS.
        /// Note: HSTS only applies in non-development environments.
        /// </summary>
        public HttpsTestConfiguration WithHstsEnabled()
        {
            SetEnvironmentVariable("HTTPS_ENABLEHSTS", "true");
            return this;
        }

        /// <summary>
        /// Configures HSTS to be disabled (default behavior).
        /// </summary>
        public HttpsTestConfiguration WithHstsDisabled()
        {
            SetEnvironmentVariable("HTTPS_ENABLEHSTS", "false");
            return this;
        }

        /// <summary>
        /// Configures the max-age value for the HSTS header in seconds.
        /// Only used when EnableHsts is true.
        /// </summary>
        /// <param name="seconds">The max-age value in seconds</param>
        public HttpsTestConfiguration WithHstsMaxAge(int seconds)
        {
            SetEnvironmentVariable("HTTPS_HSTSMAXAGESECONDS", seconds.ToString());
            return this;
        }

        /// <summary>
        /// Configures whether subdomains should be included in the HSTS policy.
        /// Only used when EnableHsts is true.
        /// </summary>
        public HttpsTestConfiguration WithHstsIncludeSubDomains()
        {
            SetEnvironmentVariable("HTTPS_HSTSINCLUDESUBDOMAINS", "true");
            return this;
        }

        /// <summary>
        /// Clears all HTTPS environment variables.
        /// Called automatically on Dispose.
        /// </summary>
        public void ClearConfiguration()
        {
            ClearEnvironmentVariable("HTTPS_ENABLED");
            ClearEnvironmentVariable("HTTPS_CERTIFICATEPATH");
            ClearEnvironmentVariable("HTTPS_CERTIFICATEPASSWORD");
            ClearEnvironmentVariable("HTTPS_REDIRECTHTTPTOHTTPS");
            ClearEnvironmentVariable("HTTPS_PORT");
            ClearEnvironmentVariable("HTTPS_ENABLEHSTS");
            ClearEnvironmentVariable("HTTPS_HSTSMAXAGESECONDS");
            ClearEnvironmentVariable("HTTPS_HSTSINCLUDESUBDOMAINS");
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

            // Prevent finalizer from running since we've already cleaned up managed resources
            GC.SuppressFinalize(this);
        }
    }
}
