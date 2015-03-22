namespace Particular.Backend.Debugging.RavenDB.Expiration
{
    using System;
    using System.ComponentModel.Composition;
    using System.Threading;
    using Raven.Abstractions;
    using Raven.Abstractions.Logging;
    using Raven.Database;
    using Raven.Database.Plugins;
    using ServiceBus.Management.Infrastructure.Settings;

    [InheritedExport(typeof(IStartupTask))]
    [ExportMetadata("Bundle", "customDocumentExpiration")]
    public class ExpiredDocumentsCleanerTask : IStartupTask, IDisposable
    {
        ILog logger = LogManager.GetLogger(typeof(ExpiredDocumentsCleanerTask));
        Timer timer;
        DocumentDatabase Database { get; set; }
        int deleteFrequencyInSeconds;
        int deletionBatchSize;
        ExpiredDocumentsCleaner cleaner = new ExpiredDocumentsCleaner();
        
        public void Execute(DocumentDatabase database)
        {
            Database = database;
            
            deletionBatchSize = Settings.ExpirationProcessBatchSize;
            deleteFrequencyInSeconds = Settings.ExpirationProcessTimerInSeconds;
            
            if (deleteFrequencyInSeconds == 0)
            {
                return;
            }

            logger.Info("Expired Documents every {0} seconds",deleteFrequencyInSeconds);
            logger.Info("Deletion Batch Size: {0}", deletionBatchSize);
            logger.Info("Retention Period: {0}", Settings.HoursToKeepMessagesBeforeExpiring);

            timer = new Timer(TimerCallback, null, TimeSpan.FromSeconds(deleteFrequencyInSeconds), Timeout.InfiniteTimeSpan);
        }

        void TimerCallback(object state)
        {
            var currentTime = SystemTime.UtcNow;
            
            try
            {
                cleaner.TryClean(currentTime, Database, deletionBatchSize);
            }
            catch (OperationCanceledException)
            {
                //Ignore
            }
            catch (Exception e)
            {
                logger.ErrorException("Error when trying to find expired documents", e);
            }
            finally
            {
                try
                {
                    timer.Change(TimeSpan.FromSeconds(deleteFrequencyInSeconds), Timeout.InfiniteTimeSpan);
                }
                catch (ObjectDisposedException)
                {
                    //Ignore 
                }
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
