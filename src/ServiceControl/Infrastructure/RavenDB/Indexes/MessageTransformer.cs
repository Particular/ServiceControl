namespace ServiceBus.Management.Infrastructure.RavenDB.Indexes
{
    using System.Linq;
    using MessageAuditing;
    using Raven.Client.Indexes;

    public class MessageTransformer : AbstractTransformerCreationTask<CommonResult>
    {
        public MessageTransformer()
        {
            TransformResults = results => from result in results
                select LoadDocument<Message>(result.Id);
        }
    }
}