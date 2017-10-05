namespace ServiceControl.LearningTransport
{
    interface ILearningTransportTransaction
    {
        string FileToProcess { get; }

        bool BeginTransaction(string incomingFilePath);

        void Commit();

        void Rollback();

        void ClearPendingOutgoingOperations();

        void Enlist(string messagePath, string messageContents);

        bool Complete();
    }
}