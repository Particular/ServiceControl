namespace ServiceControl.ExceptionGroups
{
    using System;

    public class ExceptionGroup
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public int Count { get; set; }
        public DateTime First { get; set; }
        public DateTime Last { get; set; }
    }
}