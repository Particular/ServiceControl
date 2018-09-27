namespace ServiceControl.SagaAudit
{
    using System;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    class ApiModule : BaseModule
    {
        public ApiModule()
        {
            Get["/sagas/{id}", true] = (parameters, token) => GetSagaByIdApi.Execute(this, (Guid)parameters.id);
        }

        public GetSagaByIdApi GetSagaByIdApi { get; set; }
    }
}