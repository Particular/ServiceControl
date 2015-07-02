namespace ServiceControl.Migrations
{
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Metrics;
    using Raven.Client;

    public abstract class Migration
    {
        public virtual void Setup(IDocumentStore documentStore)
        {
            DocumentStore = documentStore;
        }

        public async Task UpAsync()
        {
            var stopWatch = new Stopwatch();
            var timer = Metric.Timer(GetType().FullName, Unit.Requests);
            stopWatch.Start();
            await UpAsyncInternal();
            stopWatch.Stop();
            timer.Record(stopWatch.ElapsedMilliseconds, TimeUnit.Milliseconds);
        }

        protected abstract Task UpAsyncInternal();

        protected IDocumentStore DocumentStore { get; private set; }
    }
}