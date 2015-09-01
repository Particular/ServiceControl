using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Windows;
using System.Windows.Markup;
using Autofac;
using Caliburn.Micro;
using ReactiveUI;
using ServiceControl.Config.UI.Shell;

namespace ServiceControl.Config
{
    public class AppBootstrapper : BootstrapperBase
    {
        private IContainer container;

        public AppBootstrapper()
        {
            Initialize();
        }

        private void CreateContainer()
        {
            var containerBuilder = new ContainerBuilder();
            containerBuilder.RegisterAssemblyModules(typeof(AppBootstrapper).Assembly);
            container = containerBuilder.Build();
        }

        private void ApplyBindingCulture()
        {
            FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement), new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)));
        }

        protected override void Configure()
        {
            CreateContainer();
            ApplyBindingCulture();
            DisableRxUIDebuggerBreak();
        }

        private void DisableRxUIDebuggerBreak()
        {
            RxApp.DefaultExceptionHandler = Observer.Create(delegate (Exception ex)
            {
                Scheduler.Schedule(RxApp.MainThreadScheduler, () =>
                {
                    throw new Exception(
                        "An OnError occurred on an object (usually ObservableAsPropertyHelper) that would break a binding or command. To prevent this, Subscribe to the ThrownExceptions property of your objects",
                        ex);
                });
            });
        }

        protected override object GetInstance(Type service, string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                object result;
                if (container.TryResolve(service, out result))
                {
                    return result;
                }
            }
            else
            {
                object result;
                if (container.TryResolveNamed(key, service, out result))
                {
                    return result;
                }
            }
            throw new Exception(string.Format("Could not locate any instances of contract {0}.", key ?? service.Name));
        }

        protected override IEnumerable<object> GetAllInstances(Type service)
        {
            return container.Resolve(typeof(IEnumerable<>).MakeGenericType(new[] { service })) as IEnumerable<object>;
        }

        protected override void BuildUp(object instance)
        {
            container.InjectProperties(instance);
        }

        protected override void OnStartup(object sender, StartupEventArgs e)
        {
            DisplayRootViewFor<ShellViewModel>();
        }
    }
}