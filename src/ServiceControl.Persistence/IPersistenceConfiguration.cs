namespace ServiceControl.Persistence
{
    using System;

    public interface IPersistenceConfiguration
    {
        IPersistence Create(Func<string, Type, object> readSetting);
    }
}