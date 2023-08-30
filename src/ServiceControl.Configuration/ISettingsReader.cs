namespace ServiceBus.Management.Infrastructure.Settings
{
    using System;

    interface ISettingsReader
    {
        object Read(string root, string name, Type type, object defaultValue = null);
        bool TryRead(string root, string name, Type type, out object value);
    }
}