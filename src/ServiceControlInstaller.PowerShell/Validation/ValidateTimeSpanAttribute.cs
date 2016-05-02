namespace ServiceControlInstaller.PowerShell
{
    using System;
    using System.Management.Automation;
    
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    class ValidateTimeSpanRangeAttribute : ValidateArgumentsAttribute
    {
        public int MinimumHours { get; set; }
        public int MaximumHours { get; set; }
        
        protected override void Validate(object arguments, EngineIntrinsics engineIntrinsics)
        {
            var span = (TimeSpan) arguments;

            if (span.TotalHours < MinimumHours)
                throw new Exception(string.Format("Timespan value is lower than the minimum of {0} hours", MinimumHours));

            if (span.TotalHours > MaximumHours)
                throw new Exception(string.Format("Timespan value is greater than the maximum of {0} hours", MaximumHours));
        }
    }
}