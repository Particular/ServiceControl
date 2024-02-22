
public class QueryException : ApplicationException
{
    public QueryFailureReason Reason { get; set; }

    public QueryException(QueryFailureReason reason, string message, Exception? innerException = null) : base(message, innerException)
    {
        Reason = reason;
    }
}

public enum QueryFailureReason
{
    Auth,
    InvalidEnvironment
}
