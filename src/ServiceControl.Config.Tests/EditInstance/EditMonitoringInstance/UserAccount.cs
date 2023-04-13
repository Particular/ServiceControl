﻿namespace ServiceControl.Config.Tests.EditInstance.EditMonitoringInstance
{
    using System.Collections.Generic;
    using NUnit.Framework;
    using UI.InstanceEdit;
    using ServiceControlInstaller.Engine.Instances;
    using static MonitoringEditViewModelExtensions;

    public static class MonitoringInstanceExtensions
    {
        //public static MonitoringInstance Given_monitoring_instance()
        //{
        //    var instance = new MonitoringInstance();

        //    return instance;
        //}

        public static MonitoringInstance And_service_name(this MonitoringInstance instance, string serviceAccount)
        {
            instance.ServiceAccount = serviceAccount;

            return instance;
        }
    }

    public static class MonitoringEditViewModelExtensions
    {
        public static MonitoringEditViewModel Given_editing_an_monitoring_instance()
        {
            var viewModel = new MonitoringEditViewModel()
            {
                //This is required to notify the property change in the tests. 
                //When its figured out how to pass an instance to the constructor this will not be needed

                UseServiceAccount = false,

                UseSystemAccount = false,

                UseProvidedAccount = false
            };

            return viewModel;
        }

        public static MonitoringEditViewModel Given_editing_an_monitoring_instance(MonitoringInstance instance)
        {
            var viewModel = new MonitoringEditViewModel(instance);

            return viewModel;
        }

        public static MonitoringEditViewModel Select_user_account(this MonitoringEditViewModel viewModel)
        {
            viewModel.UseProvidedAccount = true;

            //viewModel.UseServiceAccount = false;
            //viewModel.UseSystemAccount = false;
            return viewModel;
        }

        public static MonitoringEditViewModel Select_local_system_account(this MonitoringEditViewModel viewModel)
        {
            viewModel.UseSystemAccount = true;

            //viewModel.UseServiceAccount = false;
            //viewModel.UseProvidedAccount = false;
            return viewModel;
        }

        public static MonitoringEditViewModel Select_local_service_account(this MonitoringEditViewModel viewModel)
        {
            viewModel.UseServiceAccount = true;

            //viewModel.UseProvidedAccount = false;
            //viewModel.UseSystemAccount = false;
            return viewModel;
        }

        public static MonitoringEditViewModel And_user_password_is(this MonitoringEditViewModel viewModel, string password)
        {
            viewModel.Password = password;

            return viewModel;
        }

        public static MonitoringEditViewModel And_user_account_is(this MonitoringEditViewModel viewModel, string userAccount)
        {
            viewModel.ServiceAccount = userAccount;

            return viewModel;
        }
    }

    public class EditMonitoringInstanceAccountTests
    {
        public static string ShouldBeFalse = "{0} should be false";

        public static string ShouldBeTrue = "{0} should be true";

        public static string ShouldBeEmpty = "{0} should be empty";


        [Test]
        public void Local_system_account_selected()
        {
            var changedProperties = new List<string>();

            var viewModel =
                Given_editing_an_monitoring_instance()
                    .Collect_changed_properties(changedProperties)
                    .Select_local_system_account();

            nameof(viewModel.ServiceAccount).Was_notified_of_change(changedProperties);

            nameof(viewModel.PasswordEnabled).Was_notified_of_change(changedProperties);

            Assert.IsTrue(viewModel.UseSystemAccount);

            Assert.IsFalse(viewModel.UseServiceAccount);

            Assert.IsFalse(viewModel.UseProvidedAccount);

            Assert.IsFalse(viewModel.PasswordEnabled);

            Assert.IsEmpty(viewModel.Password);

            Assert.AreEqual(viewModel.ServiceAccount, "LocalSystem");

        }

        [Test]
        public void Local_service_account_selected()
        {
            var changedProperties = new List<string>();

            var viewModel =
                Given_editing_an_monitoring_instance()
                    .Collect_changed_properties(changedProperties)
                    .Select_local_service_account();

            nameof(viewModel.ServiceAccount).Was_notified_of_change(changedProperties);

            nameof(viewModel.PasswordEnabled).Was_notified_of_change(changedProperties);


#pragma warning disable format
            //nameof(viewModel.Password).Was_notified_of_change(changedProperties);
#pragma warning restore format
            Assert.IsFalse(viewModel.UseSystemAccount);


            Assert.IsTrue(viewModel.UseServiceAccount);

            Assert.IsFalse(viewModel.UseProvidedAccount);

            Assert.IsFalse(viewModel.PasswordEnabled);

            Assert.IsEmpty(viewModel.Password);

            Assert.AreEqual(viewModel.ServiceAccount, "LocalService");
        }

        [Test]
        public void User_account_selected()
        {
            var changedProperties = new List<string>();

            var viewModel =
                Given_editing_an_monitoring_instance()
                    .Collect_changed_properties(changedProperties)
                    .Select_user_account();

            nameof(viewModel.ServiceAccount).Was_notified_of_change(changedProperties);

            nameof(viewModel.PasswordEnabled).Was_notified_of_change(changedProperties);

            nameof(viewModel.Password).Was_notified_of_change(changedProperties);

            Assert.IsFalse(viewModel.UseSystemAccount);

            Assert.IsFalse(viewModel.UseServiceAccount);

            Assert.IsTrue(viewModel.UseProvidedAccount);

            Assert.IsNull(viewModel.ServiceAccount);

            Assert.IsTrue(viewModel.PasswordEnabled);

            Assert.IsNull(viewModel.Password);
        }

        [TestCase("foo", null)]
        [TestCase("foo", "")]
        [TestCase("foo", "bar")]
        public void User_account_selected_and_user_account_entered(string userAccount, string userPassword)
        {
            var changedProperties = new List<string>();

            var viewModel =
                Given_editing_an_monitoring_instance()
                    .Collect_changed_properties(changedProperties)
                    .Select_user_account()
                    .And_user_account_is(userAccount)
                    .And_user_password_is(userPassword);

            nameof(viewModel.ServiceAccount).Was_notified_of_change(changedProperties);

            nameof(viewModel.PasswordEnabled).Was_notified_of_change(changedProperties);

            nameof(viewModel.Password).Was_notified_of_change(changedProperties);

            Assert.IsFalse(viewModel.UseSystemAccount, ShouldBeFalse, nameof(viewModel.UseSystemAccount));

            Assert.IsFalse(viewModel.UseServiceAccount, ShouldBeFalse, nameof(viewModel.UseServiceAccount));

            Assert.IsTrue(viewModel.UseProvidedAccount, ShouldBeTrue, nameof(viewModel.UseProvidedAccount));

            Assert.IsTrue(viewModel.PasswordEnabled, ShouldBeTrue, nameof(viewModel.PasswordEnabled));

            Assert.AreEqual(userAccount, viewModel.ServiceAccount);

            Assert.AreEqual(userPassword, viewModel.Password);
        }

        [TestCase("foo", null)]
        [TestCase("foo", "")]
        [TestCase("foo", "bar")]
        public void User_account_entered_then_system_account_selected(string userAccount,
            string userPassword)
        {
            var changedProperties = new List<string>();

            var viewModel =
                Given_editing_an_monitoring_instance()
                    .Collect_changed_properties(changedProperties)
                    .Select_user_account()
                    .And_user_account_is(userAccount)
                    .And_user_password_is(userPassword)
                    .Select_local_system_account();

            nameof(viewModel.ServiceAccount).Was_notified_of_change(changedProperties);

            nameof(viewModel.PasswordEnabled).Was_notified_of_change(changedProperties);

            nameof(viewModel.Password).Was_notified_of_change(changedProperties);

            Assert.IsTrue(viewModel.UseSystemAccount, ShouldBeFalse, nameof(viewModel.UseSystemAccount));

            Assert.IsFalse(viewModel.UseServiceAccount, ShouldBeTrue, nameof(viewModel.UseServiceAccount));

            Assert.IsFalse(viewModel.UseProvidedAccount, ShouldBeFalse, nameof(viewModel.UseProvidedAccount));

            Assert.IsFalse(viewModel.PasswordEnabled, ShouldBeFalse, nameof(viewModel.PasswordEnabled));

            Assert.AreEqual(viewModel.ServiceAccount, "LocalSystem");

            Assert.IsEmpty(viewModel.Password);
        }


        [TestCase("foo", null)]
        [TestCase("foo", "")]
        [TestCase("foo", "bar")]
        public void User_account_entered_then_local_service_account_selected(string userAccount,
            string userPassword)
        {
            var changedProperties = new List<string>();

            var viewModel =
                Given_editing_an_monitoring_instance()
                    .Collect_changed_properties(changedProperties)
                    .Select_user_account()
                    .And_user_account_is(userAccount)
                    .And_user_password_is(userPassword)
                    .Select_local_service_account();

            nameof(viewModel.ServiceAccount).Was_notified_of_change(changedProperties);

            nameof(viewModel.PasswordEnabled).Was_notified_of_change(changedProperties);

            nameof(viewModel.Password).Was_notified_of_change(changedProperties);

            Assert.IsFalse(viewModel.UseSystemAccount, ShouldBeFalse, nameof(viewModel.UseSystemAccount));

            Assert.IsTrue(viewModel.UseServiceAccount, ShouldBeTrue, nameof(viewModel.UseServiceAccount));

            Assert.IsFalse(viewModel.UseProvidedAccount, ShouldBeFalse, nameof(viewModel.UseProvidedAccount));

            Assert.IsFalse(viewModel.PasswordEnabled, ShouldBeFalse, nameof(viewModel.PasswordEnabled));

            Assert.AreEqual(viewModel.ServiceAccount, "LocalService");

            Assert.IsEmpty(viewModel.Password);
        }
    }
}
