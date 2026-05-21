namespace ServiceControl.Config
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Reactive;
    using System.Reactive.Concurrency;
    using System.Windows;
    using System.Windows.Markup;
    using Autofac;
    using Caliburn.Micro;
    using FluentValidation;
    using ReactiveUI;
    using ReactiveUI.Builder;
    using ServiceControl.Config.Framework;
    using ServiceControlInstaller.Engine.Validation;
    using UI.Shell;

    public class AppBootstrapper : BootstrapperBase
    {
        public AppBootstrapper()
        {
            Initialize();
        }

        void CreateContainer()
        {
            var containerBuilder = new ContainerBuilder();
            containerBuilder.RegisterAssemblyModules(typeof(AppBootstrapper).Assembly);
            container = containerBuilder.Build();
        }

        void ApplyBindingCulture()
        {
            FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement), new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)));
        }

        protected override void Configure()
        {
            CreateContainer();
            ApplyBindingCulture();
            DisableRxUIDebuggerBreak();
        }

        // ReactiveUI's default handler calls Debugger.Break() on unhandled reactive exceptions, which pauses at an unhelpful
        // internal location. This replaces it so exceptions are re-thrown on the main thread with a useful stack trace instead.
        void DisableRxUIDebuggerBreak()
        {
            _ = RxAppBuilder.CreateReactiveUIBuilder()
                .WithExceptionHandler(Observer.Create(delegate (Exception ex)
                {
                    _ = RxSchedulers.MainThreadScheduler.Schedule(() => throw new Exception(
                            "An OnError occurred on an object (usually ObservableAsPropertyHelper) that would break a binding or command. To prevent this, Subscribe to the ThrownExceptions property of your objects",
                            ex));
                }))
            .BuildApp();
        }

        protected override object GetInstance(Type service, string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                if (container.TryResolve(service, out var result))
                {
                    return result;
                }
            }
            else
            {
                if (container.TryResolveNamed(key, service, out var result))
                {
                    return result;
                }
            }

            throw new Exception($"Could not locate any instances of contract {key ?? service.Name}.");
        }

        protected override IEnumerable<object> GetAllInstances(Type service)
        {
            return container.Resolve(typeof(IEnumerable<>).MakeGenericType(service)) as IEnumerable<object>;
        }

        protected override void BuildUp(object instance)
        {
            container.InjectProperties(instance);
        }

        protected override async void OnStartup(object sender, StartupEventArgs e)
        {
            ValidatorOptions.Global.DefaultRuleLevelCascadeMode = CascadeMode.Stop;

            await DisplayRootViewForAsync<ShellViewModel>();

            var windowManager = GetInstance(typeof(IServiceControlWindowManager), null) as IServiceControlWindowManager;

            if (OldScmuCheck.OldVersionOfServiceControlInstalled(out var installedVersion))
            {
                var message = $"An old version {installedVersion} of ServiceControl Management is installed, which will not work after installing new instances. Before installing ServiceControl 5 instances, you must either uninstall the {installedVersion} instance or update it to a 4.x version at least {OldScmuCheck.MinimumScmuVersion}.";
                await windowManager.ShowMessage("Outdated Version Installed", message, acceptText: "I understand", hideCancel: true);
                Application.Current.Shutdown(-1);
            }
        }

        IContainer container;
    }
}