namespace ServiceControl.Config.Tests.Validation
{
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Moq;
    using NUnit.Framework;
    using ServiceControl.Config.UI;
    using ServiceControl.Config.UI.InstanceAdd;
    using System;
    using System.ComponentModel;
    using System.Linq;
    using System.Management.Instrumentation;
    using System.Windows.Forms;
    using System.Xml.Linq;

    public class NotEmpty
    {

        //- Example: instance name cannot be empty
        //  Given the instance name field was left empty
        //  When the user tries to save the form
        //  Then an error with the text '' should have triggered


        [Test]
        public void instance_name_cannot_be_empty()
        {
            var viewModel = new ServiceControlAddViewModel();

            viewModel.SubmitAttempted = true;

            viewModel.ConventionName = string.Empty;
          
            var errorInfo = GetErrorInfo(viewModel);
            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.IsTrue(viewModel.SubmitAttempted);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ConventionName)));

        }

        [Test]
        public void error_instance_name_cannot_be_empty_when_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel();

            viewModel.SubmitAttempted = true;

            viewModel.InstallErrorInstance = true;

            viewModel.ErrorInstanceName = string.Empty;

            var errorInfo = GetErrorInfo(viewModel);
            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ErrorInstanceName)));

        }

        [Test]
        public void error_instance_name_cannot_be_empty_when_adding_error_instance_1()
        {
            var viewModel = new ServiceControlAddViewModel();

            viewModel.SubmitAttempted = true;

            viewModel.InstallErrorInstance = true;

            viewModel.ServiceControl.DestinationPath = string.Empty;

            var errorInfo = GetErrorInfo(viewModel);
            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errorInfo2 = GetErrorInfo(viewModel.ServiceControl);
            var notifyErrorInfo2 = GetNotifyErrorInfo(viewModel.ServiceControl);

            var x = notifyErrorInfo2.GetErrors("DestinationPath");
            var y = notifyErrorInfo2.GetErrors("ServiceControl.DestinationPath");

            Assert.IsNotEmpty(notifyErrorInfo2.GetErrors("DestinationPath"));

        }

        public IDataErrorInfo GetErrorInfo(object vm)
        {

            return vm as IDataErrorInfo;
        }
        public INotifyDataErrorInfo GetNotifyErrorInfo(object vm)
        {

            return vm as INotifyDataErrorInfo;
        }
    }
}
