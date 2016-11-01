namespace ServiceControl.Recoverability
{
    public class RetryOperationsHistory
    {
        public string Id { get; set; }
        public CompletedRetryOperation[] PreviousOperations { get; set; }

        public static string MakeId()
        {
            return "RetryOperations/PreviousCompletedOperation";
        }

        public static RetryOperationsHistory CreateNew()
        {
            return new RetryOperationsHistory
            {
                PreviousOperations = new CompletedRetryOperation[0],
                Id = MakeId()
            };
        }
    }
}