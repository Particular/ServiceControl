public class IncludeInAmazonSqsTestsAttribute : IncludeInTestsAttribute
{
    protected override string Filter => "Transports.SQS";
}
