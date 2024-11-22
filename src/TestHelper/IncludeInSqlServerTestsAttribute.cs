public class IncludeInSqlServerTestsAttribute : IncludeInTestsAttribute
{
    protected override string Filter => "SqlServer";
}