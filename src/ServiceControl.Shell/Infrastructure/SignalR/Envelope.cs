namespace ServiceControl.Infrastructure.SignalR
{
    using System.Collections.Generic;

    public class Envelope
    {
        public object Message { get; set; }
        public List<string> Types { get; set; }
    }
}