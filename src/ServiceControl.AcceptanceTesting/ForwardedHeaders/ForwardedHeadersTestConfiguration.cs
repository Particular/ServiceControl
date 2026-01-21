namespace ServiceControl.AcceptanceTesting.ForwardedHeaders
{
    using System;

    /// <summary>
    /// Helper class to configure ForwardedHeaders environment variables for acceptance tests.
    /// Environment variables must be set before the ServiceControl instance starts.
    /// </summary>
    /// <remarks>
    /// Creates a new forwarded headers test configuration.
    /// </remarks>
    /// <param name="instanceType">The instance type (determines environment variable prefix)</param>
    public class ForwardedHeadersTestConfiguration(ServiceControlInstanceType instanceType) : IDisposable
    {
        readonly string envVarPrefix = EnvironmentVariablePrefixes.GetPrefix(instanceType);
        bool disposed;

        /// <summary>
        /// Configures forwarded headers to be disabled.
        /// </summary>
        public ForwardedHeadersTestConfiguration WithForwardedHeadersDisabled()
        {
            SetEnvironmentVariable("FORWARDEDHEADERS_ENABLED", "false");
            return this;
        }

        /// <summary>
        /// Configures forwarded headers to trust all proxies (default behavior).
        /// </summary>
        public ForwardedHeadersTestConfiguration WithTrustAllProxies()
        {
            SetEnvironmentVariable("FORWARDEDHEADERS_ENABLED", "true");
            SetEnvironmentVariable("FORWARDEDHEADERS_TRUSTALLPROXIES", "true");
            return this;
        }

        /// <summary>
        /// Configures forwarded headers with specific known proxies.
        /// Setting known proxies automatically disables TrustAllProxies.
        /// </summary>
        /// <param name="proxies">Comma-separated list of trusted proxy IP addresses (e.g., "127.0.0.1,::1")</param>
        public ForwardedHeadersTestConfiguration WithKnownProxies(string proxies)
        {
            SetEnvironmentVariable("FORWARDEDHEADERS_ENABLED", "true");
            SetEnvironmentVariable("FORWARDEDHEADERS_KNOWNPROXIES", proxies);
            return this;
        }

        /// <summary>
        /// Configures forwarded headers with specific known networks.
        /// Setting known networks automatically disables TrustAllProxies.
        /// </summary>
        /// <param name="networks">Comma-separated list of trusted CIDR networks (e.g., "127.0.0.0/8,::1/128")</param>
        public ForwardedHeadersTestConfiguration WithKnownNetworks(string networks)
        {
            SetEnvironmentVariable("FORWARDEDHEADERS_ENABLED", "true");
            SetEnvironmentVariable("FORWARDEDHEADERS_KNOWNNETWORKS", networks);
            return this;
        }

        /// <summary>
        /// Configures forwarded headers with both known proxies and networks.
        /// </summary>
        /// <param name="proxies">Comma-separated list of trusted proxy IP addresses</param>
        /// <param name="networks">Comma-separated list of trusted CIDR networks</param>
        public ForwardedHeadersTestConfiguration WithKnownProxiesAndNetworks(string proxies, string networks)
        {
            SetEnvironmentVariable("FORWARDEDHEADERS_ENABLED", "true");
            SetEnvironmentVariable("FORWARDEDHEADERS_KNOWNPROXIES", proxies);
            SetEnvironmentVariable("FORWARDEDHEADERS_KNOWNNETWORKS", networks);
            return this;
        }

        /// <summary>
        /// Clears all forwarded headers environment variables.
        /// Called automatically on Dispose.
        /// </summary>
        public void ClearConfiguration()
        {
            ClearEnvironmentVariable("FORWARDEDHEADERS_ENABLED");
            ClearEnvironmentVariable("FORWARDEDHEADERS_TRUSTALLPROXIES");
            ClearEnvironmentVariable("FORWARDEDHEADERS_KNOWNPROXIES");
            ClearEnvironmentVariable("FORWARDEDHEADERS_KNOWNNETWORKS");
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
