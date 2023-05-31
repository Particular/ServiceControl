public class IncludeInDefaultTestsAttribute : IncludeInTestsAttribute
{
    protected override string Filter => "Default";
}