namespace ServiceControl.ScaleOut
{
    using System.Linq;
    using Raven.Client.Indexes;
    using ServiceControl.MessageFailures.Api;

    public class ListScaleOutGroupsTransformer : AbstractTransformerCreationTask<ScaleOutGroupRegistration>
    {
        public ListScaleOutGroupsTransformer()
        {
            TransformResults = results => from result in results
                select new
                {
                    Name = result.GroupId,
                    Settings = LoadDocument<ScaleOutGroupSettings>(result.GroupId)
                };

        }
    }
}