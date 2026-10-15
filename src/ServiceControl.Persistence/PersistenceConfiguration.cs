namespace ServiceControl.Persistence;

using System;
using Configuration;

public abstract class PersistenceConfiguration
{
    const string EventRetentionPeriodKey = "EventRetentionPeriod";
    const string LegacyEventsRetentionPeriodKey = "EventsRetentionPeriod";

    protected static T GetRequiredSetting<T>(SettingsRootNamespace settingsRootNamespace, string key)
    {
        if (SettingsReader.TryRead<T>(settingsRootNamespace, key, out var value))
        {
            return value;
        }

        throw new Exception($"Setting {key} of type {typeof(T)} is required");
    }

    protected static TimeSpan ReadEventsRetentionPeriod(SettingsRootNamespace settingsRootNamespace, TimeSpan defaultValue) =>
        TimeSpan.TryParse(SettingsReader.Read<string>(settingsRootNamespace, EventRetentionPeriodKey), out var period)
            ? period
            : SettingsReader.Read(settingsRootNamespace, LegacyEventsRetentionPeriodKey, defaultValue);
}