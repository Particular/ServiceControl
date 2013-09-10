namespace ServiceControl.Alerts
{
    using System;

    /// <summary>
    /// Domain object for Alerts. Later we can add behavior to clear alerts etc.
    /// </summary>
    public class Alert
    {
        public string Id { get; set; }
        public string Description { get; set; }
        public Severity Severity { get; set; }
        public DateTime RaisedAt { get; set; }
        public string Type { get; set; }
        public string RelatedTo { get; set; } // This could be the Id of a related document, such as the FailedMessage event, which will have more information regarding this alert.
    }
}
