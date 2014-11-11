namespace ServiceControl.Infrastructure.RavenDB.Expiration
{
    using System;
    using System.ComponentModel.Composition;
    using System.Threading;
    using CompositeViews.Messages;
    using Raven.Abstractions;
    using Raven.Abstractions.Data;
    using Raven.Abstractions.Logging;
    using Raven.Database;
    using Raven.Database.Impl;
    using Raven.Database.Plugins;

    [InheritedExport(typeof(IStartupTask))]
    [ExportMetadata("Bundle", "CustomDocumentCounter")]
    public class DocumentsCounter : IStartupTask, IDisposable
    {
        private readonly ILog logger = LogManager.GetLogger(typeof(DocumentsCounter));
        private Timer timer;
        DocumentDatabase Database { get; set; }
        string indexName;
        
        
        public void Execute(DocumentDatabase database)
        {
            Database = database;
            indexName = new MessagesViewIndex().IndexName;
            timer = new Timer(TimerCallback, null, TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(60));
        }

        void TimerCallback(object state)
        {
            var currentTime = SystemTime.UtcNow;
            var query = new IndexQuery
            {
                Cutoff = currentTime,
                Query = "Status:3 OR Status:4"
            };
            
            try
            {
                using (DocumentCacher.SkipSettingDocumentsInDocumentCache())
                using (Database.DisableAllTriggersForCurrentThread())
                using (var cts = new CancellationTokenSource())
                {
                    logger.Debug("Document count according to {0} is {1}", indexName, Database.Query(indexName, query, cts.Token).TotalResults);
                }
            }
            catch (OperationCanceledException)
            {
                //Ignore
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            if (timer != null)
            {
                using (var waitHandle = new ManualResetEvent(false))
                {
                    timer.Dispose(waitHandle);

                    waitHandle.WaitOne();
                }
            }
        }
    }
}
