namespace ServiceControl.EndpointPlugin.CustomChecks
{
    using System;
    using System.Linq;

    public abstract class CustomCheck
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



        public void ReportFailed(string failureDescription)
        {
            ReportToBackend(CustomCheckResult.Failed(failureDescription), CustomCheckId, Category);
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
            ServiceControlBackend.Send(new ReportCustomCheck
            {
                CustomCheckId = customCheckId,
                Category = category,
                Result = result,
                ReportedAt = DateTime.UtcNow
            });
        }
    }

    public  class ReportCustomCheck
    {
        public string CustomCheckId { get; set; }
        public string Category { get; set; }
        public CustomCheckResult Result { get; set; }
        public DateTime ReportedAt { get; set; }
    }

    public interface IServiceControlBackend
    {
        void Send(ReportCustomCheck reportCustomCheck);
    }

    public  abstract class PeriodicCustomCheck : CustomCheck
    {
        protected virtual TimeSpan Interval
        {
            get
            {
                return TimeSpan.FromMinutes(1);
            }
        }

        public abstract void PerformCheck();
    }

    public class CustomCheckResult
    {
        public bool HasFailed { get; set; }
        public string FailureReason { get; set; }

        public static CustomCheckResult Ok
        {
            get
            {
                return new CustomCheckResult();
            }

        }

        public static CustomCheckResult Failed(string reason)
        {
            return new CustomCheckResult
            {
                HasFailed = true,
                FailureReason = reason
            };


        }

    }
}