namespace ServiceControl.MessageFailures.Api
{
    using Nancy;
    using Nancy.ModelBinding;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;
    using ServiceControl.ScaleOut;

    public class UpdateScaleOutGroupSettings : BaseModule
    {
        public UpdateScaleOutGroupSettings()
        {
            Patch["/scaleoutgroup/{id}/settings"] = parameters =>
            {
                string groupId = parameters.id;
                var data = this.Bind<ScaleOutGroupSettingsModel>();
                using (var session = Store.OpenSession())
                {
                    var item = session.Load<ScaleOutGroupSettings>(groupId) ?? new ScaleOutGroupSettings(groupId);

                    item.ConnectAutomatically = data.ConnectAutomatically;
                    item.MinimumConnected = data.MinimumConnected;
                    item.ReconnectAutomatically = data.ReconnectAutomatically;

                    session.Store(item);
                    session.SaveChanges();
                }

                return HttpStatusCode.OK;
            };
        }

        class ScaleOutGroupSettingsModel
        {
            public bool ReconnectAutomatically { get; set; }
            public bool ConnectAutomatically { get; set; }
            public int MinimumConnected { get; set; }
        }
    }
}