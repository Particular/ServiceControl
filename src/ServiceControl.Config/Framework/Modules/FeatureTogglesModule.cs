namespace ServiceControl.Config.Framework.Modules
{
    using System;
    using System.Linq;
    using System.Reflection;
    using Autofac;
    using Autofac.Core;
    using Autofac.Core.Registration;
    using Autofac.Core.Resolving.Pipeline;
    using Module = Autofac.Module;

    public class FeatureTogglesModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<FeatureToggles>().SingleInstance();
            builder.RegisterType<ToggleFeaturesFromConfig>().AsImplementedInterfaces();
            builder.RegisterType<FeatureToggleDefaults>().AsImplementedInterfaces();
        }

        protected override void AttachToComponentRegistration(IComponentRegistryBuilder componentRegistryBuilder, IComponentRegistration registration)
        {
            registration.PipelineBuilding += (sender, builder) =>
                builder.Use(PipelinePhase.Activation, (context, callback) =>
                {
                    callback(context);
                    OnComponentActivated(context);
                });
        }

        void OnComponentActivated(ResolveRequestContext e)
        {
            var instanceType = e.Instance.GetType();

            if (instanceType == typeof(FeatureToggles))
            {
                return;
            }

            var featureToggles = e.Resolve<FeatureToggles>();

            var featureProperties = from prop in instanceType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                                    let attribute = prop.GetCustomAttributes<FeatureToggleAttribute>().SingleOrDefault()
                                    where attribute != null
                                    select new
                                    {
                                        attribute.Feature,
                                        Property = prop
                                    };

            foreach (var featureProp in featureProperties)
            {
                if (featureProp.Property.PropertyType != typeof(bool))
                {
                    throw new InvalidOperationException($"Prerelease Property must be a bool {instanceType.FullName}.{featureProp.Property.Name}");
                }

                if (!featureProp.Property.CanWrite)
                {
                    throw new InvalidOperationException($"Prerelease Property must be writeable {instanceType.FullName}.{featureProp.Property.Name}");
                }

                featureProp.Property.SetValue(e.Instance, featureToggles.IsEnabled(featureProp.Feature));
            }
        }
    }
}