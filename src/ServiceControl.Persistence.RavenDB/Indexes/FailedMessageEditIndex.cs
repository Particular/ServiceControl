namespace ServiceControl.Persistence.RavenDB.Indexes
{
    using System.Linq;
    using Raven.Client.Documents.Indexes;
    using ServiceControl.Persistence.Recoverability.Editing;

    class FailedMessageEditIndex : AbstractIndexCreationTask<FailedMessageEdit>
    {
        public FailedMessageEditIndex()
        {
            Map = edits =>
                from edit in edits
                select new
                {
                    edit.EditId,
                    edit.FailedMessageId
                };
        }

        public class SortAndFilterOptions
        {
            public string EditId { get; set; }
            public string FailedMessageId { get; set; }
        }
    }
}