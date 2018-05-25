namespace Particular.ServiceControl.Upgrade
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Features;

    public class StaleIndexAfterUpgrade : Feature
    {
        public StaleIndexAfterUpgrade()
        {
            EnableByDefault();
            RegisterStartupTask<CheckerTask>();
        }
        
        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent<StaleIndexChecker>(DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<StaleIndexInfoStore>(DependencyLifecycle.SingleInstance);
        }
        
        public class CheckerTask : FeatureStartupTask 
        {
            private string directory;
            private Task checkTask;
            private CancellationTokenSource tokenSource;
            private StaleIndexChecker staleIndexChecker;
            private StaleIndexInfoStore staleIndexInfoStore;

            public CheckerTask(StaleIndexChecker staleIndexChecker, StaleIndexInfoStore staleIndexInfoStore) : this(staleIndexChecker, staleIndexInfoStore, null){}
            
            protected CheckerTask(StaleIndexChecker staleIndexChecker, StaleIndexInfoStore staleIndexInfoStore, string baseDirectory)
            {
                this.staleIndexInfoStore = staleIndexInfoStore;
                this.staleIndexChecker = staleIndexChecker;
                if (baseDirectory == null)
                {
                    this.directory = AppDomain.CurrentDomain.BaseDirectory;
                }
                else
                {
                    this.directory = baseDirectory;
                }
            }
            
            protected override void OnStop()
            {
                tokenSource?.Cancel();
                checkTask?.GetAwaiter().GetResult();
            }

            protected override void OnStart()
            {
                var strings = Directory.GetFiles(directory, "*.upgrade");
                var latestUpgrade = strings.Select(f => Convert.ToInt64(Path.GetFileNameWithoutExtension(f))).OrderByDescending(x => x).ToArray();
                if (latestUpgrade.Length == 0)
                {
                    return;
                }

                tokenSource = new CancellationTokenSource();
                var fileTime = latestUpgrade[0];
                var latest = DateTime.FromFileTimeUtc(fileTime);
                checkTask = Task.Run(async () =>
                {
                    while (!tokenSource.IsCancellationRequested)
                    {
                        if (await staleIndexChecker.Check(latest, tokenSource.Token).ConfigureAwait(false))
                        {
                            File.Delete(Path.Combine(this.directory, $"{fileTime}.upgrade"));
                            staleIndexInfoStore.Store(StaleIndexInfoStore.NotInProgress);
                            break;
                        }

                        staleIndexInfoStore.Store(new StaleIndexInfo { InProgress = true, StartedAt = latest });
                    }
                });
            }
        }
    }
}