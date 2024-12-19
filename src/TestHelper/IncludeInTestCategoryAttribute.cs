public class IncludeInTestCategoryAttribute : IncludeInTestsAttribute
{
    public IncludeInTestCategoryAttribute(string filter)
    {
        Filter = filter;
    }

    protected override string Filter { get; }
}