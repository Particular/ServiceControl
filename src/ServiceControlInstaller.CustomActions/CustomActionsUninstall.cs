namespace ServiceControlInstaller.CustomActions
{
    using System;
    using Engine.Instances;
    using Engine.Unattended;
    using Microsoft.Deployment.WindowsInstaller;

    public class CustomActionsUninstall
    {
        [CustomAction]
        public static ActionResult ServiceControlInstanceCount(Session session)
        {
            var instanceCount = InstanceFinder.ServiceControlInstances().Count +
                                InstanceFinder.MonitoringInstances().Count;
            session["SCINSTANCECOUNT"] = instanceCount.ToString();
            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult ServiceControlUnattendedRemoval(Session session)
        {
            var logger = new MSILogger(session);
            var removeInstancesPropertyValue = session["REMOVEALLINSTANCESANDDATA"];
            if (string.IsNullOrWhiteSpace(removeInstancesPropertyValue))
                return ActionResult.NotExecuted;

            switch (removeInstancesPropertyValue.ToUpper())
            {
                case "YES":
                case "TRUE":
                    break;
                default:
                    return ActionResult.NotExecuted;
            }

            if (InstanceFinder.ServiceControlInstances().Count == 0)
            {
                return ActionResult.Success;
            }

            var unattendedInstaller = new UnattendServiceControlInstaller(logger, session["APPDIR"]);
            foreach (var instance in InstanceFinder.ServiceControlInstances())
            {
                try
                {
                    unattendedInstaller.Delete(instance.Name, true, true);
                }
                catch (Exception ex)
                {
                    logger.Error($"Error thrown when removing instance {instance.Name} - {ex}");
                }
            }

            return ActionResult.Success;
        }
    }
}