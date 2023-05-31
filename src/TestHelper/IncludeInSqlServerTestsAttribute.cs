public class IncludeInSqlServerTestsAttribute : IncludeInTestsAttribute
{
    protected override string Filter => "Transports.SqlServer";
}
