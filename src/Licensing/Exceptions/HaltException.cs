class HaltException : ApplicationException
{
    public int ExitCode { get; set; }

    public HaltException(HaltReason reason, string message, Exception? innerException = null) : base(message, innerException)
    {
        ExitCode = (int)reason;
    }

    public HaltException(QueryException queryException)
        : this(GetHaltReason(queryException.Reason), queryException.Message, queryException)
    {

    }

    static HaltReason GetHaltReason(QueryFailureReason reason) => reason switch
    {
        QueryFailureReason.Auth => HaltReason.Auth,
        QueryFailureReason.InvalidEnvironment => HaltReason.InvalidEnvironment,
        _ => HaltReason.RuntimeError
    };
}

enum HaltReason
{
    UserCancellation = 1,
    OutputFile = 2,
    MissingConfig = 3,
    InvalidConfig = 4,
    InvalidEnvironment = 5,
    RuntimeError = 6,
    Auth = 7,
}