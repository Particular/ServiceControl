//namespace ServiceControl.Config.Commands
//{
//    using System;
//    using Caliburn.Micro;
//    using Events;
//    using Framework;
//    using Framework.Commands;
//    using ServiceControlInstaller.Engine.Instances;
//    using UI.InstanceDetails;
//    using UI.InstanceEdit;

//    class EditServiceControlAuditInstanceCommand : AbstractCommand<InstanceDetailsViewModel>
//    {
//        public EditServiceControlAuditInstanceCommand(IWindowManagerEx windowManager, Func<ServiceControlAuditInstance, ServiceControlEditViewModel> editViewModel, IEventAggregator eventAggregator) : base(CanEditInstance)
//        {
//            this.windowManager = windowManager;
//            this.editViewModel = editViewModel;
//            this.eventAggregator = eventAggregator;
//        }

//        static bool CanEditInstance(InstanceDetailsViewModel viewModel)
//        {
//            var instance = (ServiceControlAuditInstance)viewModel.ServiceInstance;
//            return instance.VersionHasServiceControlAuditFeatures;
//        }

//        public override void Execute(InstanceDetailsViewModel viewModel)
//        {
//            var editVM = editViewModel((ServiceControlAuditInstance)viewModel.ServiceInstance);

//            if (windowManager.ShowInnerDialog(editVM) ?? false)
//            {
//                editVM.UpdateInstanceFromViewModel((ServiceControlAuditInstance)viewModel.ServiceInstance);
//                eventAggregator.PublishOnUIThread(new RefreshInstances());
//            }
//        }

//        readonly Func<ServiceControlAuditInstance, ServiceControlEditViewModel> editViewModel;
//        readonly IEventAggregator eventAggregator;
//        readonly IWindowManagerEx windowManager;
//    }

//    class EditServiceControlInstanceCommand : AbstractCommand<InstanceDetailsViewModel>
//    {
//        public EditServiceControlInstanceCommand(IWindowManagerEx windowManager, Func<ServiceControlInstance, ServiceControlEditViewModel> editViewModel, IEventAggregator eventAggregator) : base(CanEditInstance)
//        {
//            this.windowManager = windowManager;
//            this.editViewModel = editViewModel;
//            this.eventAggregator = eventAggregator;
//        }

//        static bool CanEditInstance(InstanceDetailsViewModel viewModel)
//        {
//            var instance = (ServiceControlInstance)viewModel.ServiceInstance;
//            return instance.VersionHasServiceControlAuditFeatures;
//        }

//        public override void Execute(InstanceDetailsViewModel viewModel)
//        {
//            var editVM = editViewModel((ServiceControlInstance)viewModel.ServiceInstance);

//            if (windowManager.ShowInnerDialog(editVM) ?? false)
//            {
//                editVM.UpdateInstanceFromViewModel((ServiceControlInstance)viewModel.ServiceInstance);
//                eventAggregator.PublishOnUIThread(new RefreshInstances());
//            }
//        }

//        readonly Func<ServiceControlInstance, ServiceControlEditViewModel> editViewModel;
//        readonly IEventAggregator eventAggregator;
//        readonly IWindowManagerEx windowManager;
//    }
//}