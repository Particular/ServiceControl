namespace ServiceControl.Recoverability.Groups
{
    using System;

    public class FailureGroup
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Type { get; set; }
        public int Count { get; set; }
        public DateTime First { get; set; }
        public DateTime Last { get; set; }
    }
}