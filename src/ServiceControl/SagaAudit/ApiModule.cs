namespace ServiceControl.SagaAudit
{
    using System;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;
    using ServiceControl.CompositeViews.Messages;

    public class ApiModule : BaseModule
    {
        public ListSagasApi ListSagasApi { get; set; }
        public GetSagaByIdApi GetSagaByIdApi { get; set; }

        public ApiModule()
        {
            Get["/sagas/{id}", true] = (parameters, token) => GetSagaByIdApi.Execute(this, (Guid) parameters.id);

            Get["/sagas", true] = (_, token) => ListSagasApi.Execute(this, NoInput.Instance);
        }
    }
}