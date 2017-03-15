
namespace ServiceControl.Recoverability
{
    using System;

    public class HistoricArchiveOperation
    {
        public string RequestId { get; set; }
        public ArchiveType ArchiveType { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime CompletionTime { get; set; }
        public string Originator { get; set; }
        public bool Failed { get; set; }
        public int NumberOfMessagesProcessed { get; set; }
    }
}