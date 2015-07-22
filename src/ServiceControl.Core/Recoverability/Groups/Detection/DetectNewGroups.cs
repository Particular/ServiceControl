namespace ServiceControl.Recoverability.Groups.Detection
{
    using System;
    using NServiceBus;

    public class DetectNewGroups : ICommand
    {
        public DateTimeOffset StartOfWindow { get; set; }
        public DateTimeOffset EndOfWindow { get; set; }
    }
}