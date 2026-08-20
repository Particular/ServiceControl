namespace ServiceControl.Recoverability
{
    using System;

    public class FailureGroupView
    {
        public required string Id { get; set; }
        public required string Title { get; set; }
        public required string Type { get; set; }
        public int Count { get; set; }
        public string? Comment { get; set; }
        public DateTime First { get; set; }
        public DateTime Last { get; set; }
    }
}