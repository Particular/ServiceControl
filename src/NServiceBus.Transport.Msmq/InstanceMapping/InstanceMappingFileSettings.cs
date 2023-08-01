namespace NServiceBus
{
    using System;
    using Configuration.AdvancedExtensibility;
    using Settings;
    using Transport.Msmq;

    /// <summary>
    /// Allows configuring file-based instance mappings.
    /// </summary>
    public class InstanceMappingFileSettings : ExposeSettings
    {
        /// <summary>
        /// Creates new instance of <see cref="InstanceMappingFileSettings"/>.
        /// </summary>
        public InstanceMappingFileSettings(SettingsHolder settings)
            : base(settings)
        {
        }

        /// <summary>
        /// Specifies the interval between data refresh attempts.
        /// The default value is 30 seconds.
        /// </summary>
        /// <param name="refreshInterval">Refresh interval. Valid values must be between 1 second and less than 1 day.</param>
        public InstanceMappingFileSettings RefreshInterval(TimeSpan refreshInterval)
        {
            if (refreshInterval < TimeSpan.FromSeconds(1))
            {
                throw new ArgumentOutOfRangeException(nameof(refreshInterval), "Value must be at least 1 second.");
            }
            if (refreshInterval > TimeSpan.FromDays(1))
            {
                throw new ArgumentOutOfRangeException(nameof(refreshInterval), "Value must be less than 1 day.");
            }

            this.GetSettings().Set(InstanceMappingFileFeature.CheckIntervalSettingsKey, refreshInterval);
            return this;
        }

        /// <summary>
        /// Specifies the path and file name for the instance mapping XML. The default is <code>instance-mapping.xml</code>.
        /// </summary>
        /// <param name="filePath">The relative or absolute file path to the instance mapping XML file.</param>
        public InstanceMappingFileSettings FilePath(string filePath)
        {
            Guard.AgainstNullAndEmpty(nameof(filePath), filePath);
            var result = Uri.TryCreate(filePath, UriKind.RelativeOrAbsolute, out var uriPath);
            if (!result)
            {
                throw new ArgumentException("Invalid format", nameof(filePath));
            }

            this.GetSettings().Set(InstanceMappingFileFeature.PathSettingsKey, uriPath);
            return this;
        }

        /// <summary>
        /// Specifies the uri for the instance mapping XML.
        /// </summary>
        /// <param name="uriPath">The absolute uri to the instance mapping XML.</param>
        public InstanceMappingFileSettings Path(Uri uriPath)
        {
            Guard.AgainstNull(nameof(uriPath), uriPath);
            this.GetSettings().Set(InstanceMappingFileFeature.PathSettingsKey, uriPath);
            return this;
        }

        /// <summary>
        /// Turns on strict schema validation for the instance mapping XML.
        /// Unknown attribtutes will trigger a schema validation exception.
        /// </summary>
        public InstanceMappingFileSettings EnforceStrictSchemaValidation()
        {
            this.GetSettings().Set(InstanceMappingFileFeature.StrictSchemaValidationKey, true);
            return this;
        }
    }
}