namespace Particular.ServiceControl.Licensing
{
    using Particular.Licensing;

    public class ActiveLicense
    {
        public bool IsValid { get; set; }
        public bool HasExpired { get; set; }
        internal License Details { get; set; }
    }
}