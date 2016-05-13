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
                throw new Exception($"Timespan value is lower than the minimum of {MinimumHours} hours");

            if (span.TotalHours > MaximumHours)
                throw new Exception($"Timespan value is greater than the maximum of {MaximumHours} hours");
        }
    }
}