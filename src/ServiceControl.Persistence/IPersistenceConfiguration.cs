namespace ServiceControl.Persistence
{
    using System;

    public interface IPersistenceConfiguration
    {
        IPersistenceSettings CreateSettings(Func<string, Type, (bool exists, object value)> tryReadSetting);
        IPersistence Create(IPersistenceSettings settings);
    }
}