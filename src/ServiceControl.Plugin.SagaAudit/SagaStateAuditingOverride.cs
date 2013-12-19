namespace ServiceControl.EndpointPlugin.SagaState
{
    using NServiceBus.Pipeline;
    using NServiceBus.Pipeline.Contexts;
    using NServiceBus.Sagas;

    // ReSharper disable CSharpWarnings::CS0618
    class SagaStateAuditingOverride : PipelineOverride
    {
        public override void Override(BehaviorList<HandlerInvocationContext> behaviorList)
        {
            behaviorList.InsertBefore<SagaPersistenceBehavior, CaptureSagaStateBehavior>();
        }
        public override void Override(BehaviorList<SendPhysicalMessageContext> behaviorList)
        {
            behaviorList.Add<CaptureSagaResultingMessagesBehavior>();
        }
    }
    // ReSharper restore CSharpWarnings::CS0618
}