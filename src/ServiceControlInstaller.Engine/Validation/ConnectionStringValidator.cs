namespace ServiceControlInstaller.Engine.Validation
{
    using System;
    using System.Data.Common;
    using System.Linq;
    using Accounts;

    class ConnectionStringValidator
    {
        ConnectionStringValidator(string connectionString, string serviceAccount)
        {
            this.connectionString = connectionString;
            this.serviceAccount = serviceAccount;
        }

        public static void Validate(IServiceControlAuditInstance instance)
        {
            var validator = new ConnectionStringValidator(instance.ConnectionString, instance.ServiceAccount);
            if (instance.TransportPackage.Name == "SQLServer")
            {
                validator.CheckMsSqlConnectionString();
            }
            else if (instance.TransportPackage.Name == "PostgreSQL")
            {
                validator.CheckPostgreSqlConnectString();
            }
        }

        public static void Validate(IServiceControlInstance instance)
        {
            var validator = new ConnectionStringValidator(instance.ConnectionString, instance.ServiceAccount);
            if (instance.TransportPackage.Name == "SQLServer")
            {
                validator.CheckMsSqlConnectionString();
            }
            else if (instance.TransportPackage.Name == "PostgreSQL")
            {
                validator.CheckPostgreSqlConnectString();
            }
        }

        public static void Validate(IMonitoringInstance instance)
        {
            var validator = new ConnectionStringValidator(instance.ConnectionString, instance.ServiceAccount);
            if (instance.TransportPackage.Name == "SQLServer")
            {
                validator.CheckMsSqlConnectionString();
            }
            else if (instance.TransportPackage.Name == "PostgreSQL")
            {
                validator.CheckPostgreSqlConnectString();
            }
        }

        void CheckMsSqlConnectionString()
        {
            try
            {
                //Check  validity of connection string. This will throw if invalid
                var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };

                //Check that localsystem is not used when integrated security is enabled
                if (builder.ContainsKey("Integrated Security"))
                {
                    var integratedSecurity = (string)builder["Integrated Security"];
                    var enabledValues = new[]
                    {
                        "true",
                        "yes",
                        "sspi"
                    };
                    if (enabledValues.Any(p => p.Equals(integratedSecurity, StringComparison.OrdinalIgnoreCase)))
                    {
                        var account = UserAccount.ParseAccountName(serviceAccount);
                        if (account.IsLocalSystem())
                        {
                            throw new EngineValidationException("Invalid service account for this connection string. The connection string has integrated security enabled but localsystem service has been selected.");
                        }
                    }
                }
            }
            catch (ArgumentException argumentException)
            {
                throw new EngineValidationException($"Connection String is invalid - {argumentException.Message}");
            }
        }

        void CheckPostgreSqlConnectString()
        {
            try
            {
                //Check  validity of connection string. This will throw if invalid
                var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
            }
            catch (ArgumentException argumentException)
            {
                throw new EngineValidationException($"Connection String is invalid - {argumentException.Message}");
            }
        }

        string connectionString;
        string serviceAccount;
    }
}