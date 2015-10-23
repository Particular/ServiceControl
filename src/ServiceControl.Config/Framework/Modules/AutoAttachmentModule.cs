using System;
using System.Linq;
using Autofac;
using Autofac.Core;
using Caliburn.Micro;

namespace ServiceControl.Config.Framework.Modules
{
    public class AutoAttachmentModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterAssemblyTypes(AssemblySource.Instance.ToArray())
              .Where(type => type.Name.EndsWith("Attachment") && type.IsAssignableTo<IAttachment>())
              .AsSelf()
              .InstancePerDependency();
        }

        protected override void AttachToComponentRegistration(IComponentRegistry componentRegistry, IComponentRegistration registration)
        {
            registration.Activated += OnComponentActivated;
        }

        private void OnComponentActivated(object sender, ActivatedEventArgs<object> e)
        {
            var vmType = e.Instance.GetType();

            if (!vmType.FullName.EndsWith("ViewModel"))
                return;

            var attachmentBaseType = typeof(Attachment<>).MakeGenericType(vmType);

            var attachments = ThisAssembly.GetTypes()
                .Where(t => InheritsFrom(t, attachmentBaseType) && e.Context.IsRegistered(t))
                .Select(t => (IAttachment)e.Context.Resolve(t));

            foreach (var attachment in attachments)
            {
                attachment.AttachTo(e.Instance);
            }
        }

        private bool InheritsFrom(Type type, Type baseType)
        {
            return type.BaseType != null && (type.BaseType == baseType || InheritsFrom(type.BaseType, baseType));
        }
    }
}