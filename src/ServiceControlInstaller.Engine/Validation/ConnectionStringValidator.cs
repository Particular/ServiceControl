namespace ServiceControlInstaller.Engine.Validation
{
    using System;
    using System.Linq;
    using System.Data.Common;
    using System.Data.SqlClient;
    using ServiceControlInstaller.Engine.Accounts;
    using ServiceControlInstaller.Engine.Instances;

    class ConnectionStringValidator
    {
        IServiceControlInstance instance;

        public static void Validate(IServiceControlInstance instance)
        {
            var validator = new ConnectionStringValidator(instance);
            if (instance.TransportPackage == "SQLServer")
            {
                validator.CheckMsSqlConnectionString();
            }
        }

        ConnectionStringValidator(IServiceControlInstance instance)
        {
            this.instance = instance;
        }
        
        void CheckMsSqlConnectionString()
        {
            try
            {
                //Check  validity of connection string. This will throw if invalid
                var builder = new DbConnectionStringBuilder{ConnectionString = instance.ConnectionString};  

                //Check that localsystem is not used when integrated security is enabled
                if (builder.ContainsKey("Integrated Security"))
                {
                    var integratedSecurity = (string) builder["Integrated Security"];
                    var enabledValues= new []{"true","yes","sspi"};
                    if (enabledValues.Any(p => p.Equals(integratedSecurity, StringComparison.OrdinalIgnoreCase)))
                    {
                        var account = UserAccount.ParseAccountName(instance.ServiceAccount);
                        if (account.IsLocalSystem())
                        {
                            throw new EngineValidationException("Invalid service account for this connection string. The connection string has integrated security enabled but localsystem service has been selected.");
                        }
                    }
                }

                //Attempt to connect to DB 
                using (var s = new SqlConnection(builder.ConnectionString))
                {
                    s.Open();
                }
            }
            catch (ArgumentException argumentException)
            {
                throw new EngineValidationException(string.Format("Connection String is invalid - {0}", argumentException.Message));
            }
            catch (SqlException sqlEx)
            {
                throw new EngineValidationException(string.Format("SQL connection failed - {0}", sqlEx.Message));
            }
        }
    }
}
