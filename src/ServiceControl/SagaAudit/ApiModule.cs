namespace ServiceControl.SagaAudit
{
    using System;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class ApiModule : BaseModule
    {
        public GetSagaByIdApi GetSagaByIdApi { get; set; }

        public ApiModule()
        {
            Get["/sagas/{id}", true] = (parameters, token) => GetSagaByIdApi.Execute(this, (Guid) parameters.id);
        }
    }
}