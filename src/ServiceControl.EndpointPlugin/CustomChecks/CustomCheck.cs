namespace ServiceControl.EndpointPlugin.CustomChecks
{
    using System;
    using System.Linq;
    using Messages.CustomChecks;
    using Messages.Operations.ServiceControlBackend;

    public abstract class CustomCheck : ICustomCheck
    {
        public IServiceControlBackend ServiceControlBackend { get; set; }

        public virtual string Category
        {
            get
            {
                return GetType().Namespace.Split('.').Last().Replace("Checks", "");
            }
        }

        public void ReportOk()
        {
            ReportToBackend(CheckResult.Ok, CustomCheckId, Category);
        }


        public void ReportFailed(string failureReason)
        {
            ReportToBackend(CheckResult.Failed(failureReason), CustomCheckId, Category);
        }

        public string CustomCheckId
        {
            get
            {
                return GetType().FullName;
            }
        }
        
        void ReportToBackend(CheckResult result, string customCheckId, string category)
        {
            ServiceControlBackend.Send(new ReportCustomCheckResult
            {
                CustomCheckId = customCheckId,
                Category = category,
                Result = result,
                ReportedAt = DateTime.UtcNow
            });
        }
    }
}