namespace ServiceControl.Plugin.SagaAudit
{
    using NServiceBus.Features;
    using NServiceBus.Pipeline;
    using NServiceBus.Pipeline.Contexts;
    using NServiceBus.Sagas;
    // ReSharper disable CSharpWarnings::CS0618
    class SagaStateAuditingOverride : PipelineOverride
    {
        public override void Override(BehaviorList<HandlerInvocationContext> behaviorList)
        {
            if (!Feature.IsEnabled<Features.SagaAudit>())
            {
                return;
            }

            behaviorList.InsertBefore<SagaPersistenceBehavior, CaptureSagaStateBehavior>();
        }
        public override void Override(BehaviorList<SendPhysicalMessageContext> behaviorList)
        {
            if (!Feature.IsEnabled<Features.SagaAudit>())
            {
                return;
            }

            behaviorList.Add<CaptureSagaResultingMessagesBehavior>();
        }
    }
    // ReSharper restore CSharpWarnings::CS0618
}