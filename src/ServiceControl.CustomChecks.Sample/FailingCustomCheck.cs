namespace ServiceControl.CustomChecks.Sample
{
    using Plugin.CustomChecks;

    class FailingCustomCheck : CustomCheck
    {
        public FailingCustomCheck()
            : base("FailingCustomCheck", "CustomCheck")
        {
            ReportFailed("Some reason");
        }
    }
}