namespace ServiceControl.Config
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Configuration;
    using Autofac;

    [AttributeUsage(AttributeTargets.Property)]
    public class FeatureToggleAttribute : Attribute
    {
        public FeatureToggleAttribute(string feature)
        {
            Feature = feature;
        }

        public string Feature { get; }
    }

    public static class Feature
    {
        public const string MonitoringInstances = "MonitoringInstances";
        public const string LicenseChecks = "LicenseChecks";
    }

    public class FeatureToggles
    {
        public bool IsEnabled(string feature)
        {
            return features.Contains(feature);
        }

        public void Enable(string feature)
        {
            features.Add(feature);
        }

        HashSet<string> features = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
    }

    public class FeatureToggleDefaults : IStartable
    {
        public FeatureToggleDefaults(FeatureToggles featureToggles)
        {
            this.featureToggles = featureToggles;
        }

        public void Start()
        {
            featureToggles.Enable(Feature.MonitoringInstances);
            featureToggles.Enable(Feature.LicenseChecks);
        }

        FeatureToggles featureToggles;
    }

    public class ToggleFeaturesFromConfig : IStartable
    {
        public ToggleFeaturesFromConfig(FeatureToggles featureToggles)
        {
            this.featureToggles = featureToggles;
        }

        public void Start()
        {
            ConfigureFeatures(ConfigurationManager.AppSettings);
        }

        void ConfigureFeatures(NameValueCollection appSettings)
        {
            foreach (var key in appSettings.AllKeys)
            {
                if (key.StartsWith(EnableFeaturePrefix, StringComparison.InvariantCultureIgnoreCase))
                {
                    ConfigureFeature(key.Substring(EnableFeaturePrefix.Length), appSettings[key]);
                }
            }
        }

        void ConfigureFeature(string feature, string value)
        {
            if (bool.TryParse(value, out var result))
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

        FeatureToggles featureToggles;
        const string EnableFeaturePrefix = "Enable-Feature:";
    }
}