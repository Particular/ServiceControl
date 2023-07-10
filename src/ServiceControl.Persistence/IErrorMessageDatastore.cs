namespace ServiceControl.Persistence
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using CompositeViews.Messages;
    using Infrastructure;

    public interface IErrorMessageDataStore
    {
        Task<QueryResult<IList<MessagesView>>> GetAllMessages(PagingInfo pagingInfo, SortInfo sortInfo, bool includeSystemMessages);
    }
}
