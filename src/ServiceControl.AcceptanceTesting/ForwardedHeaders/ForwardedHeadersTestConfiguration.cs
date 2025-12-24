namespace ServiceControl.AcceptanceTesting.ForwardedHeaders
{
    using System;

    /// <summary>
    /// Helper class to configure ForwardedHeaders environment variables for acceptance tests.
    /// Environment variables must be set before the ServiceControl instance starts.
    /// </summary>
    public class ForwardedHeadersTestConfiguration : IDisposable
    {
        readonly string envVarPrefix;
        bool disposed;

        /// <summary>
        /// Creates a new forwarded headers test configuration.
        /// </summary>
        /// <param name="instanceType">The instance type (determines environment variable prefix)</param>
        public ForwardedHeadersTestConfiguration(ServiceControlInstanceType instanceType)
        {
            envVarPrefix = instanceType switch
            {
                ServiceControlInstanceType.Primary => "SERVICECONTROL_",
                ServiceControlInstanceType.Audit => "SERVICECONTROL_AUDIT_",
                ServiceControlInstanceType.Monitoring => "MONITORING_",
                _ => throw new ArgumentOutOfRangeException(nameof(instanceType))
            };
        }

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
        /// Applies the configuration by ensuring environment variables are set.
        /// This should be called before the ServiceControl instance starts.
        /// </summary>
        public void Apply()
        {
            // Configuration is already applied via the With* methods
            // This method exists for explicit apply semantics if needed
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

        void SetEnvironmentVariable(string name, string value)
        {
            Environment.SetEnvironmentVariable(envVarPrefix + name, value);
        }

        void ClearEnvironmentVariable(string name)
        {
            Environment.SetEnvironmentVariable(envVarPrefix + name, null);
        }

        public void Dispose()
        {
            if (!disposed)
            {
                ClearConfiguration();
                disposed = true;
            }
        }
    }

    /// <summary>
    /// Identifies the ServiceControl instance type for environment variable prefix selection.
    /// </summary>
    public enum ServiceControlInstanceType
    {
        Primary,
        Audit,
        Monitoring
    }
}
