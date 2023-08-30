namespace ServiceBus.Management.Infrastructure.Settings
{
    using System;

    class EnvironmentVariableSettingsReader : ISettingsReader
    {
        public object Read(string root, string name, Type type, object defaultValue = default)
        {
            return TryRead(root, name, type, out var value)
                ? value
                : defaultValue;
        }

        public bool TryRead(string root, string name, Type type, out object value)
        {
            if (TryReadVariable(type, out value, $"{root}/{name}"))
            {
                return true;
            }
            // Azure container instance compatibility:
            if (TryReadVariable(type, out value, $"{root}_{name}".Replace('.', '_')))
            {
                return true;
            }

            value = default;
            return false;
        }

        static bool TryReadVariable(Type type, out object value, string fullKey)
        {
            var environmentValue = Environment.GetEnvironmentVariable(fullKey);

            if (environmentValue != null)
            {
                environmentValue = Environment.ExpandEnvironmentVariables(environmentValue);
                value = Convert.ChangeType(environmentValue, type);
                return true;
            }

            value = default;
            return false;
        }
    }
}