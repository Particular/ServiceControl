namespace ServiceControl.Plugin.CustomChecks
{
    using System;
    using Internal;
    using NServiceBus;
    using Messages;
    using NServiceBus.Transports;

    public abstract class CustomCheck : ICustomCheck
    {

        static CustomCheck()
        {
            hostInfo = HostInformationRetriever.RetrieveHostInfo();
        }

        protected CustomCheck(string id, string category)
        {
            this.category = category;
            this.id = id;
        }

        public string Category
        {
            get { return category; }
        }

        public void ReportPass()
        {
            ReportToBackend(CheckResult.Pass);
        }


        public void ReportFailed(string failureReason)
        {
            ReportToBackend(CheckResult.Failed(failureReason));
        }

        public string Id
        {
            get { return id; }
        }

        void ReportToBackend(CheckResult result)
        {
            var sender = Configure.Instance.Builder.Build<ISendMessages>();

            new ServiceControlBackend(sender).Send(new ReportCustomCheckResult
            {
                HostId = hostInfo.HostId,
                Host = hostInfo.Name,
                EndpointName = Configure.EndpointName,
                CustomCheckId = id,
                Category = category,
                HasFailed = result.HasFailed,
                FailureReason = result.FailureReason,
                ReportedAt = DateTime.UtcNow
            });
        }

        readonly string category;
        readonly string id;
        static readonly HostInformation hostInfo;

    }
}