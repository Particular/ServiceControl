namespace ServiceControl.Persistence
{
    using System;

    public interface IPersistenceConfiguration
    {
        PersistenceSettings CreateSettings(Func<string, Type, (bool exists, object value)> tryReadSetting);
        IPersistence Create(PersistenceSettings settings);
    }
}