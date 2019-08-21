namespace ServiceControl.Monitoring
{
    using System;
    using System.Collections.Specialized;

    class SettingsReader
    {
        NameValueCollection values;

        public SettingsReader(NameValueCollection values)
        {
            this.values = values;
        }

        public T Read<T>(string key, T defaultValue = default(T))
        {
            var value = values[key];
            if (value == null)
                return defaultValue;
            return (T)Convert.ChangeType(value, typeof(T));
        }
    }
}
