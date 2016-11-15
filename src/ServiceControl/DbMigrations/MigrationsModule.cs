namespace Particular.ServiceControl.DbMigrations
{
    using Autofac;

    public class MigrationsModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);
            builder.RegisterType<MigrationsManager>().SingleInstance();
            builder.RegisterAssemblyTypes(ThisAssembly).As<IMigration>();
        }
    }
}