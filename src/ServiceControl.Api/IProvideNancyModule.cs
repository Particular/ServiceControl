namespace ServiceControl.Api
{
    using Nancy;

    public interface IProvideNancyModule
    {
        INancyModule NancyModule { get; }
    }
}