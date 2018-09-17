﻿namespace ServiceControl.Recoverability
{
    using System;

    class ArchiveOperation // raven
    {
        public string Id { get; set; }
        public string RequestId { get; set; }
        public string GroupName { get; set; }
        public ArchiveType ArchiveType { get; set; }
        public int TotalNumberOfMessages { get; set; }
        public int NumberOfMessagesArchived { get; set; }
        public DateTime Started { get; set; }
        public int NumberOfBatches { get; set; }
        public int CurrentBatch { get; set; }

        public static string MakeId(string requestId, ArchiveType archiveType)
        {
            return $"ArchiveOperations/{(int)archiveType}/{requestId}";
        }
    }
}