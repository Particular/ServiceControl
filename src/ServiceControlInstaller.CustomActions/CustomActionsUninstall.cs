namespace ServiceControlInstaller.CustomActions
{
    using System;
    using Microsoft.Deployment.WindowsInstaller;
    using ServiceControlInstaller.Engine.Instances;
    using ServiceControlInstaller.Engine.Unattended;

    public class CustomActionsUninstall
    {
        [CustomAction]
        public static ActionResult ServiceControlInstanceCount(Session session)
        {
            var instanceCount = ServiceControlInstance.Instances().Count;
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
                case "YES" :
                case "TRUE" :
                    break;
                default:
                    return ActionResult.NotExecuted;
            }

            if (ServiceControlInstance.Instances().Count == 0)
            {
                return ActionResult.Success;
            }
            
            var unattendedInstaller = new UnattendInstaller(logger, session["APPDIR"]);
            foreach (var instance in ServiceControlInstance.Instances())
            {
                try
                {
                    unattendedInstaller.Delete(instance.Name, true, true);
                }
                catch (Exception ex)
                {
                    logger.Error(string.Format("Error thrown when removing instance {0} - {1}", instance.Name, ex));
                }
            }
            return ActionResult.Success;
        }
    }
}
