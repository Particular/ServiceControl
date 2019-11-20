namespace ServiceControl.Transports.ASB
{
    using System;
    using System.Linq;

    class ConnectionStringPartRemover
    {
        public static string Remove(string connectionString, string partName)
        {
            var parts = connectionString.Split(new[] {';'}, StringSplitOptions.RemoveEmptyEntries);

            var filteredParts = parts.Where(p => !p.StartsWith($"{partName}=", StringComparison.InvariantCultureIgnoreCase));

            var newConnectionString = string.Join(";", filteredParts);

            return newConnectionString;
        }
    }
}