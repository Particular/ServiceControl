namespace ServiceControl.Alerts
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Domain object for Alerts. Later we can add behavior to clear alerts etc.
    /// </summary>
    public class Alert
    {
        public string Id { get; set; }
        public string Description { get; set; }
        public Severity Severity { get; set; }
        public DateTime RaisedAt { get; set; }
        public string Tags { get; set; }
        public ICollection<string> RelatedTo { get; set; } // This could be the Id of a related document, such as the FailedMessage event, which will have more information regarding this alert.
    }
}
