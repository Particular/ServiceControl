public class IncludeInMsmqTestsAttribute : IncludeInTestsAttribute
{
    protected override string Filter => "MSMQ";
}