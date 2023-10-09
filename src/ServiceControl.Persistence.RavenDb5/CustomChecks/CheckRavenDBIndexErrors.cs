namespace ServiceControl
{
    using System;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using NServiceBus.CustomChecks;
    using NServiceBus.Logging;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Operations.Indexes;

    class CheckRavenDBIndexErrors : CustomCheck
    {
        public CheckRavenDBIndexErrors(IDocumentStore store)
            : base("Error Database Index Errors", "ServiceControl Health", TimeSpan.FromMinutes(5))
        {
            this.store = store;
        }

        public override Task<CheckResult> PerformCheck()
        {
            var response = store.Maintenance.Send(new GetIndexErrorsOperation());

            // Filter response as RavenDB5 will return entries without errors
            var indexErrors = response
                .Where(x => x.Errors.Any())
                .ToArray();

            if (indexErrors.Length == 0)
            {
                return CheckResult.Pass;
            }

            var text = new StringBuilder();
            text.AppendLine("Detected RavenDB index errors, please start maintenance mode and resolve the following issues:");

            foreach (var indexError in indexErrors)
            {
                foreach (var indexingError in indexError.Errors)
                {
                    text.AppendLine($"- Index [{indexError.Name}] error: {indexError.Name} (Action: {indexingError.Action},  Doc: {indexingError.Document}, At: {indexingError.Timestamp})");
                }
            }

            text.AppendLine().AppendLine("See: https://docs.particular.net/search?q=servicecontrol+troubleshooting");

            var message = text.ToString();
            Logger.Error(message);
            return CheckResult.Failed(message);
        }

        static readonly ILog Logger = LogManager.GetLogger<CheckRavenDBIndexLag>();

        readonly IDocumentStore store;
    }
}
