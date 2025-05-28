namespace ServiceControl.Contracts.Operations
{
    public class ExceptionDetails
    {
        public string ExceptionType { get; set; }
        public string Message { get; set; }
        public string Source { get; set; }
        public string StackTrace { get; set; }
        public bool IsEmpty() => string.IsNullOrEmpty(ExceptionType) && string.IsNullOrEmpty(Message) && string.IsNullOrEmpty(Source) && string.IsNullOrEmpty(StackTrace);
    }
}