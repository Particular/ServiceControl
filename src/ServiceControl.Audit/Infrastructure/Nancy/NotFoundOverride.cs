namespace ServiceControl.Audit.Infrastructure.Nancy
{
    using global::Nancy;
    using global::Nancy.ErrorHandling;

    class NotFoundOverride : IStatusCodeHandler
    {
        public bool HandlesStatusCode(HttpStatusCode statusCode, NancyContext context)
        {
            return statusCode == HttpStatusCode.NotFound;
        }

        public void Handle(HttpStatusCode statusCode, NancyContext context)
        {
        }
    }
}