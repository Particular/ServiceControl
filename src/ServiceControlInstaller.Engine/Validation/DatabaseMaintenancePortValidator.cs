﻿namespace ServiceControlInstaller.Engine.Validation
{
    using Ports;

    public class DatabaseMaintenancePortValidator
    {
        public static void Validate(IDatabaseMaintenanceSupport instance)
        {
            if (!instance.DatabaseMaintenancePort.HasValue)
            {
                throw new EngineValidationException("Database maintenance port number is not set");
            }

            if (instance.DatabaseMaintenancePort is < 1 or > 49151)
            {
                throw new EngineValidationException("Database maintenance port number is not between 1 and 49151");
            }

            if (!PortUtils.CheckAvailable(instance.DatabaseMaintenancePort.Value))
            {
                throw new EngineValidationException($"Port {instance.DatabaseMaintenancePort} is not available");
            }
        }
    }
}