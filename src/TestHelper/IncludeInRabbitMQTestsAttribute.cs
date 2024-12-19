public class IncludeInRabbitMQTestsAttribute : IncludeInTestsAttribute
{
    protected override string Filter => "RabbitMQ";
}