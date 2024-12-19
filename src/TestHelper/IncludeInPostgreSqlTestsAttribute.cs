public class IncludeInPostgreSqlTestsAttribute : IncludeInTestsAttribute
{
    protected override string Filter => "PostgreSql";
}