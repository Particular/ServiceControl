internal class HaltException : ApplicationException
{
    public int ExitCode { get; set; }

    public HaltException(HaltReason reason, string message, Exception innerException = null) : base(message, innerException) => ExitCode = (int)reason;
}

internal enum HaltReason
{
    UserCancellation = 1,
    OutputFile = 2,
    MissingConfig = 3,
    InvalidConfig = 4,
    InvalidEnvironment = 5,
    RuntimeError = 6,
    Auth = 7
}