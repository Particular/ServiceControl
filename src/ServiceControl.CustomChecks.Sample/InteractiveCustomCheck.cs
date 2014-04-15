namespace ServiceControl.CustomChecks.Sample
{
    using System;
    using NServiceBus;
    using Plugin.CustomChecks;

    class InteractiveCustomCheck : PeriodicCheck
    {
        public InteractiveCustomCheck()
            : base("InteractiveCustomCheck", "CustomCheck",TimeSpan.FromSeconds(5))
        {
            
        }

        public static bool ShouldFail { get; set; }
        public override CheckResult PerformCheck()
        {
            return ShouldFail ? CheckResult.Failed("User asked me to fail") : CheckResult.Pass;
        }
    }

    class CommandLineHook: IWantToRunWhenBusStartsAndStops
    {
        public void Start()
        {
            while (true)
            {
                Console.Out.WriteLine("Hit any key to toggle the interactive check (max delay == 5 s)");

                Console.ReadKey();

                InteractiveCustomCheck.ShouldFail =  !InteractiveCustomCheck.ShouldFail;
            }
        }

        public void Stop()
        {
            
        }
    }
}