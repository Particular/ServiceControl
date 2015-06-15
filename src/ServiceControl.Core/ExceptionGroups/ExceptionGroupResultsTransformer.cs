namespace ServiceControl.ExceptionGroups
{
    using System.Linq;
    using Raven.Client.Indexes;

    public class ExceptionGroupResultsTransformer : AbstractTransformerCreationTask<MessageFailureHistory_ByExceptionGroup.ReduceResult>
    {
        public ExceptionGroupResultsTransformer()
        {
            TransformResults = results => from result in results
                select new ExceptionGroup
                {
                    Id = result.ExceptionType, 
                    Title = result.ExceptionType, 
                    First = result.First, 
                    Last = result.Last, 
                    Count = result.Count
                };
        }
    }
}