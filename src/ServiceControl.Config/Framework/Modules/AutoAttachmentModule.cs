namespace ServiceControl.Config.Framework.Modules
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Autofac;
    using Autofac.Core;
    using Autofac.Core.Registration;
    using Autofac.Core.Resolving.Pipeline;
    using Caliburn.Micro;

    public class AutoAttachmentModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterAssemblyTypes(AssemblySource.Instance.ToArray())
                .Where(type => type.Name.EndsWith("Attachment"))
                .AsClosedTypesOf(typeof(Attachment<>))
                .InstancePerDependency();
        }

        protected override void AttachToComponentRegistration(IComponentRegistryBuilder componentRegistry, IComponentRegistration registration)
        {
            registration.PipelineBuilding +=
                (sender, pipeline) => pipeline.Use(new WireAttachmentsMiddleware());
        }

        class WireAttachmentsMiddleware : IResolveMiddleware
        {
            public void Execute(ResolveRequestContext context, Action<ResolveRequestContext> next)
            {
                next(context);

                var vmType = context.Instance?.GetType();

                if (vmType == null || !vmType.FullName.EndsWith("ViewModel"))
                {
                    return;
                }

                var attachmentBaseType = typeof(Attachment<>).MakeGenericType(vmType);
                var collectionType = typeof(IEnumerable<>).MakeGenericType(attachmentBaseType);
                var attachments = (IEnumerable<IAttachment>)context.Resolve(collectionType);

                foreach (var attachment in attachments)
                {
                    attachment.AttachTo(context.Instance);
                }
            }

            public PipelinePhase Phase => PipelinePhase.Activation;
        }
    }
}