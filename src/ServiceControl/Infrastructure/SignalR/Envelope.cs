namespace ServiceControl.Infrastructure.SignalR
{
    public class Envelope
    {
        public object Message { get; set; }
        public string Type { get; set; }
    }
}