using System;

namespace ServiceControl.Config
{
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Configuration;
    using Autofac;

    [AttributeUsage(AttributeTargets.Property)]
    public class FeatureToggleAttribute : Attribute
    {
        public string Feature { get; }

        public FeatureToggleAttribute(string feature)
        {
            Feature = feature;
        }
    }

    public static class Feature
    {
        public const string MonitoringInstances = "MonitoringInstances";
        public const string LicenseChecks = "LicenseChecks";
    }

    public class FeatureToggles
    {
        private HashSet<string> features = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

        public bool IsEnabled(string feature)
        {
            return features.Contains(feature);
        }

        public void Enable(string feature)
        {
            features.Add(feature);
        }
    }

    public class FeatureToggleDefaults : IStartable
    {
        private FeatureToggles featureToggles;

        public FeatureToggleDefaults(FeatureToggles featureToggles)
        {
            this.featureToggles = featureToggles;
        }

        public void Start()
        {
            featureToggles.Enable(Feature.MonitoringInstances);
            featureToggles.Enable(Feature.LicenseChecks);
        }
    }

    public class ToggleFeaturesFromConfig : IStartable
    {
        private FeatureToggles featureToggles;
        private const string EnableFeaturePrefix = "Enable-Feature:";

        public ToggleFeaturesFromConfig(FeatureToggles featureToggles)
        {
            this.featureToggles = featureToggles;
        }

        public void Start()
        {
            ConfigureFeatures(ConfigurationManager.AppSettings);
        }

        private void ConfigureFeatures(NameValueCollection appSettings)
        {
            foreach (var key in appSettings.AllKeys)
            {
                if (key.StartsWith(EnableFeaturePrefix, StringComparison.InvariantCultureIgnoreCase))
                {
                    ConfigureFeature(key.Substring(EnableFeaturePrefix.Length), appSettings[key]);
                }
            }
        }

        private void ConfigureFeature(string feature, string value)
        {
            bool result;
            if (bool.TryParse(value, out result))
            {
                if (result)
                {
                    featureToggles.Enable(feature);
                }
            }
            else
            {
                throw new Exception($"Config setting {EnableFeaturePrefix}{feature} should be a boolean. Cannot parse {value}");
            }

        }
    }
}
