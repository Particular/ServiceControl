namespace ServiceControl.Recoverability
{
    using System.Linq;
    using MessageFailures;
    using Raven.Client.Indexes;

    class GroupCommentIndex : AbstractIndexCreationTask<GroupComment>
    {
        public GroupCommentIndex()
        {
            Map = docs => docs.Select(gc => new GroupComment { Id = gc.Id, Comment = gc.Comment });

            DisableInMemoryIndexing = true;
        }
    }
}