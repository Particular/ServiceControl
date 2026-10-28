namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceControl.Persistence.BodyStorage;

    class RecordingBodyStorage : IAuditBodyStoragePersistence
    {
        public List<(string BodyId, DateTime CreatedOn, byte[] Body, string ContentType)> Written { get; } = [];

        public List<DateTime> DeletedHours { get; } = [];

        public List<DateTime> DeletedDays { get; } = [];

        public Exception WriteFailure { get; set; }

        public Exception DeleteFailure { get; set; }

        public TaskCompletionSource PendingWrite { get; set; }

        public Task WriteBody(string bodyId, DateTime createdOn, ReadOnlyMemory<byte> body, string contentType, CancellationToken cancellationToken = default)
        {
            if (PendingWrite != null)
            {
                return PendingWrite.Task;
            }

            if (WriteFailure != null)
            {
                return Task.FromException(WriteFailure);
            }

            Written.Add((bodyId, createdOn, body.ToArray(), contentType));
            return Task.CompletedTask;
        }

        public Task<MessageBodyFileResult> ReadBody(string bodyId, DateTime createdOn, CancellationToken cancellationToken = default) =>
            Task.FromResult<MessageBodyFileResult>(null);

        public Task DeleteBodiesForHour(DateTime hour, CancellationToken cancellationToken = default)
        {
            if (DeleteFailure != null)
            {
                return Task.FromException(DeleteFailure);
            }

            DeletedHours.Add(hour);
            return Task.CompletedTask;
        }

        public Task DeleteBodiesForDay(DateTime day, CancellationToken cancellationToken = default)
        {
            if (DeleteFailure != null)
            {
                return Task.FromException(DeleteFailure);
            }

            DeletedDays.Add(day);
            return Task.CompletedTask;
        }

        public string[] WrittenIds => [.. Written.Select(write => write.BodyId)];
    }
}
