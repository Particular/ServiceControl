namespace ServiceControlInstaller.CustomActions
{
    using System;
    using Microsoft.Deployment.WindowsInstaller;
    using ServiceControlInstaller.Engine.Instances;
    
    public class CustomActionsUninstall
    {
        [CustomAction]
        public static ActionResult ServiceControlInstanceCount(Session session)
        {
            var instanceCount = ServiceControlInstance.Instances().Count;
            // ReSharper disable once StringLiteralTypo
            session["SCINSTANCECOUNT"] = instanceCount.ToString();
            return ActionResult.Success;
        }
    }
}
