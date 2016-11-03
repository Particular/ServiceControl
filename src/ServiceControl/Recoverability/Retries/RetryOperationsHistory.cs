namespace ServiceControl.Recoverability
{
    public class RetryOperationsHistory
    {
        public string Id { get; set; }
        public CompletedRetryOperation[] PreviousFullyCompletedOperations { get; set; }

        public static string MakeId()
        {
            return "RetryOperations/PreviousCompletedOperation";
        }

        public static RetryOperationsHistory CreateNew()
        {
            return new RetryOperationsHistory
            {
                PreviousFullyCompletedOperations = new CompletedRetryOperation[0],
                Id = MakeId()
            };
        }
    }
}