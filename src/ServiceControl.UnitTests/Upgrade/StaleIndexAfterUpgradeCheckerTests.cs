namespace ServiceControl.UnitTests.Upgrade
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Particular.ServiceControl.Upgrade;

    [TestFixture]
    public class StaleIndexAfterUpgradeCheckerTests
    {
        [Test]
        public async Task Should_delete_upgrade_file_once_indexing_reached_cutoff()
        {
            var currentDirectory = TestContext.CurrentContext.TestDirectory;

            // cleanup
            foreach (var file in Directory.GetFiles(currentDirectory, "*.upgrade"))
            {
                File.Delete(file);
            }
            
            File.Create(Path.Combine(currentDirectory, $"{DateTime.UtcNow.ToFileTimeUtc()}.upgrade")).Close();
            var latestTime = DateTime.UtcNow.AddMinutes(1);
            var latest = Path.Combine(currentDirectory, $"{latestTime.ToFileTimeUtc()}.upgrade");
            File.Create(latest).Close();
            
            var staleIndexChecker = new TestableStaleIndexChecker();
            var indexStore = new StaleIndexInfoStore();
            staleIndexChecker.Result = false; // stale
            var checker = new TestableCheckerTask(staleIndexChecker, indexStore, currentDirectory);
            StaleIndexInfo? infoInProgress, infoDone;
            try
            {
                checker.Start();
                await Task.Delay(1000); // evil time based for now
                infoInProgress = indexStore.Get();

                await Task.Delay(1000); // evil time based for now
                staleIndexChecker.Result = true;

                await Task.Delay(1000); // evil time based for now
                infoDone = indexStore.Get();
            }
            finally
            {
                checker.Stop();
            }
            
            Assert.False(File.Exists(latest));
            Assert.AreEqual(staleIndexChecker.CutoffTime, latestTime);
            Assert.IsTrue(infoInProgress.Value.InProgress);
            Assert.IsFalse(infoDone.Value.InProgress);
        }
        
        [Test]
        public async Task Should_indicate_not_in_progress_even_if_file_cant_be_deleted()
        {
            var currentDirectory = TestContext.CurrentContext.TestDirectory;

            // cleanup
            foreach (var file in Directory.GetFiles(currentDirectory, "*.upgrade"))
            {
                File.Delete(file);
            }
            
            File.Create(Path.Combine(currentDirectory, $"{DateTime.UtcNow.ToFileTimeUtc()}.upgrade")).Close();
            var latestTime = DateTime.UtcNow.AddMinutes(1);
            var latest = Path.Combine(currentDirectory, $"{latestTime.ToFileTimeUtc()}.upgrade");
            File.Create(latest).Close();
            var latestFile = File.Open(latest, FileMode.Open, FileAccess.Read); // leave it open to prevent deletion
            
            var staleIndexChecker = new TestableStaleIndexChecker();
            var indexStore = new StaleIndexInfoStore();
            staleIndexChecker.Result = false; // stale
            var checker = new TestableCheckerTask(staleIndexChecker, indexStore, currentDirectory);
            StaleIndexInfo? infoInProgress, infoDone;
            try
            {
                checker.Start();
                await Task.Delay(1000); // evil time based for now
                infoInProgress = indexStore.Get();

                await Task.Delay(1000); // evil time based for now
                staleIndexChecker.Result = true;

                await Task.Delay(1000); // evil time based for now
                infoDone = indexStore.Get();
            }
            finally
            {
                checker.Stop();
                latestFile.Close();
            }
            
            Assert.AreEqual(staleIndexChecker.CutoffTime, latestTime);
            Assert.IsTrue(infoInProgress.Value.InProgress);
            Assert.IsFalse(infoDone.Value.InProgress);
        }

        class TestableCheckerTask : StaleIndexAfterUpgrade.CheckerTask
        {
            public TestableCheckerTask(StaleIndexChecker indexChecker, StaleIndexInfoStore staleIndexInfoStore, string baseDirectory) : base(indexChecker, staleIndexInfoStore, null, baseDirectory)
            {
            }

            public void Start()
            {
                OnStart();
            }
            
            public void Stop()
            {
                OnStop();
            }
        }
        
        class TestableStaleIndexChecker : StaleIndexChecker 
        {
            public TestableStaleIndexChecker() : base(null)
            {
            }

            public bool Result { get; set; }
            
            public DateTime CutoffTime { get; private set; }

            public override Task<bool> IsReindexingInComplete(DateTime cutOffTime, CancellationToken cancellationToken)
            {
                CutoffTime = cutOffTime;
                return Task.FromResult(Result);
            }
        }
    }
}