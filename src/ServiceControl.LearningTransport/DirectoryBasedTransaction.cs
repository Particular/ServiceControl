namespace ServiceControl.LearningTransport
{
    using System.Collections.Concurrent;
    using System.IO;
    using NServiceBus.Logging;

    class DirectoryBasedTransaction : ILearningTransportTransaction
    {
        private readonly PathCalculator.EndpointBasePaths endpointPaths;

        public DirectoryBasedTransaction(PathCalculator.EndpointBasePaths endpointPaths, string transactionId)
        {
            this.endpointPaths = endpointPaths;
            this.transactionDir = Path.Combine(endpointPaths.Pending, transactionId);
            this.commitDir = Path.Combine(endpointPaths.Committed, transactionId);
        }

        public string FileToProcess { get; private set; }

        public bool BeginTransaction(string incomingFilePath)
        {
            Directory.CreateDirectory(transactionDir);
            FileToProcess = Path.Combine(transactionDir, Path.GetFileName(incomingFilePath));

            try
            {
                FileOps.Move(incomingFilePath, FileToProcess);
            }
            catch (IOException ex)
            {
                log.Debug($"Failed to move {incomingFilePath} to {FileToProcess}", ex);
                return false;
            }

            //seem like File.Move is not atomic at least within the same process so we need this extra check
            return File.Exists(FileToProcess);
        }

        public void Commit()
        {
            Directory.Move(transactionDir, commitDir);
            committed = true;
        }

        public void Rollback()
        {
            //rollback by moving the file back to the main dir
            FileOps.Move(FileToProcess, Path.Combine(endpointPaths.Header, Path.GetFileName(FileToProcess)));
            Directory.Delete(transactionDir, true);
        }

        public void ClearPendingOutgoingOperations()
        {
            OutgoingFile _;
            while (outgoingFiles.TryDequeue(out _)) { }
        }

        public void Enlist(string messagePath, string messageContents)
        {
            var inProgressFileName = Path.GetFileNameWithoutExtension(messagePath) + ".out";

            var txPath = Path.Combine(transactionDir, inProgressFileName);
            var committedPath = Path.Combine(commitDir, inProgressFileName);

            outgoingFiles.Enqueue(new OutgoingFile(committedPath, messagePath));

            File.WriteAllText(txPath, messageContents);
        }

        public bool Complete()
        {
            if (!committed)
            {
                return false;
            }

            OutgoingFile outgoingFile;
            while (outgoingFiles.TryDequeue(out outgoingFile))
            {
                FileOps.Move(outgoingFile.TxPath, outgoingFile.TargetPath);
            }

            Directory.Delete(commitDir, true);

            return true;
        }

        public static void RecoverPartiallyCompletedTransactions(PathCalculator.EndpointBasePaths endpointPaths)
        {
            if (Directory.Exists(endpointPaths.Pending))
            {
                foreach (var transactionDir in new DirectoryInfo(endpointPaths.Pending).EnumerateDirectories())
                {
                    new DirectoryBasedTransaction(endpointPaths, transactionDir.Name)
                        .RecoverPending();
                }
            }

            if (Directory.Exists(endpointPaths.Committed))
            {
                foreach (var transactionDir in new DirectoryInfo(endpointPaths.Committed).EnumerateDirectories())
                {
                    new DirectoryBasedTransaction(endpointPaths, transactionDir.Name)
                        .RecoverCommitted();
                }
            }
        }

        void RecoverPending()
        {
            var pendingDir = new DirectoryInfo(transactionDir);

            //only need to move the incoming file
            foreach (var file in pendingDir.EnumerateFiles(TxtFileExtension))
            {
                FileOps.Move(file.FullName, Path.Combine(endpointPaths.Header, file.Name));
            }

            pendingDir.Delete(true);
        }

        void RecoverCommitted()
        {
            var committedDir = new DirectoryInfo(commitDir);

            //for now just rollback the completed ones as well. We could consider making this smarter in the future
            // but its good enough for now since duplicates is a possibility anyway
            foreach (var file in committedDir.EnumerateFiles(TxtFileExtension))
            {
                FileOps.Move(file.FullName, Path.Combine(endpointPaths.Header, file.Name));
            }

            committedDir.Delete(true);
        }

        bool committed;

        string transactionDir;
        string commitDir;

        ConcurrentQueue<OutgoingFile> outgoingFiles = new ConcurrentQueue<OutgoingFile>();

        const string TxtFileExtension = "*.txt";

        static ILog log = LogManager.GetLogger<DirectoryBasedTransaction>();

        class OutgoingFile
        {
            public OutgoingFile(string txPath, string targetPath)
            {
                TxPath = txPath;
                TargetPath = targetPath;
            }

            public string TxPath { get; }
            public string TargetPath { get; }
        }
    }
}
