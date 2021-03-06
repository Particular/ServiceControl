﻿namespace ServiceControl.Hosting.Commands
{
    using System.Threading.Tasks;
    using Particular.ServiceControl;
    using Particular.ServiceControl.Commands;
    using Particular.ServiceControl.Hosting;

    class MaintCommand : AbstractCommand
    {
        public override Task Execute(HostArguments args) => MaintenanceBootstrapper.Run(args);
    }
}