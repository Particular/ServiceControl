namespace ServiceControl.MessageFailures.Api
{
    using Nancy;
    using Nancy.ModelBinding;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class ConnectToScaleOutGroup : BaseModule
    {
        public ConnectToScaleOutGroup()
        {
            Post["/scaleoutgroups/{id}/connect"] = parameters =>
            {
                string groupId = parameters.id;

                var address = this.Bind<string>();

                if (string.IsNullOrEmpty(address))
                {
                    return HttpStatusCode.BadRequest;
                }

                using (var session = Store.OpenSession())
                {
                    var scaleOutGroupRegistration = new ScaleOutGroupRegistration(groupId, address);
                   
                    session.Store(scaleOutGroupRegistration);
                    session.SaveChanges();

                    return HttpStatusCode.NoContent;
                }
            };
        }
    }
}