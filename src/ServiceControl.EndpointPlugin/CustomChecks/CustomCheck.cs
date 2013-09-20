namespace ServiceControl.EndpointPlugin.CustomChecks
{
    using System;
    using System.Linq;
    using Infrastructure.ServiceControlBackend;
    using Internal;

    public abstract class CustomCheck : ICustomCheck
    {
        public IServiceControlBackend ServiceControlBackend { get; set; }

        protected virtual string Category
        {
            get
            {
                return GetType().Namespace.Split('.').Last().Replace("Checks", "");
            }
        }

        public void ReportOk()
        {
            ReportToBackend(CustomCheckResult.Ok, CustomCheckId, Category);
        }


        public void ReportFailed(string failureReason)
        {
            ReportToBackend(CustomCheckResult.Failed(failureReason), CustomCheckId, Category);
        }

        string CustomCheckId
        {
            get
            {
                return GetType().FullName;
            }
        }
        
        void ReportToBackend(CustomCheckResult result, string customCheckId, string category)
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