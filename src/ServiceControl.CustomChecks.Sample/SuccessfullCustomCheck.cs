namespace ServiceControl.CustomChecks.Sample
{
    using Plugin.CustomChecks;

    class SuccessfullCustomCheck : CustomCheck
    {
        public SuccessfullCustomCheck()
            : base("SuccessfullCustomCheck", "CustomCheck")
        {
            ReportPass();
        }
    }
}