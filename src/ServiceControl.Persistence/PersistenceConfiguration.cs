namespace ServiceControl.Persistence;

using System;
using Configuration;

public abstract class PersistenceConfiguration
{
    public const string DataSpaceRemainingThresholdKey = "DataSpaceRemainingThreshold";
    public const string MinimumStorageLeftRequiredForIngestionKey = "MinimumStorageLeftRequiredForIngestion";

    protected void LoadCommonConfiguration(PersistenceSettings settings, SettingsRootNamespace settingsRootNamespace)
    {
        settings.MinimumStorageLeftRequiredForIngestion = SettingsReader.Read(settingsRootNamespace, MinimumStorageLeftRequiredForIngestionKey, PersistenceSettings.MinimumStorageLeftRequiredForIngestionDefault);
        settings.DataSpaceRemainingThreshold = SettingsReader.Read(settingsRootNamespace, DataSpaceRemainingThresholdKey, PersistenceSettings.DataSpaceRemainingThresholdDefault);
    }

    protected static T GetRequiredSetting<T>(SettingsRootNamespace settingsRootNamespace, string key)
    {
        if (SettingsReader.TryRead<T>(settingsRootNamespace, key, out var value))
        {
            return value;
        }

        throw new Exception($"Setting {key} of type {typeof(T)} is required");
    }
}