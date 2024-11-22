namespace ServiceControl.Config.Tests.EditInstance.EditErrorInstance
{
    using System.Collections.Generic;
    using NUnit.Framework;
    using ServiceControlInstaller.Engine.Instances;
    using UI.InstanceEdit;
    using static ServiceControlEditErrorViewModelExtensions;

    public static class ServiceControlInstanceExtensions
    {
        //public static ServiceControlInstance Given_service_control_instance()
        //{
        //    var instance = new ServiceControlInstance();

        //    return instance;
        //}

        public static ServiceControlInstance And_service_name(this ServiceControlInstance instance, string serviceAccount)
        {
            instance.ServiceAccount = serviceAccount;

            return instance;
        }
    }

    public static class ServiceControlEditErrorViewModelExtensions
    {
        public static ServiceControlEditViewModel Given_editing_an_error_instance()
        {
            var viewModel = new ServiceControlEditViewModel()
            {
                //This is required to notify the property change in the tests. 
                //When its figured out how to pass an instance to the constructor this will not be needed

                UseServiceAccount = false,

                UseSystemAccount = false,

                UseProvidedAccount = false
            };

            return viewModel;
        }

        public static ServiceControlEditViewModel Given_editing_an_error_instance(ServiceControlInstance instance)
        {
            var viewModel = new ServiceControlEditViewModel(instance);

            return viewModel;
        }

        public static ServiceControlEditViewModel Select_user_account(this ServiceControlEditViewModel viewModel)
        {
            viewModel.UseProvidedAccount = true;

            return viewModel;
        }

        public static ServiceControlEditViewModel Select_local_system_account(this ServiceControlEditViewModel viewModel)
        {
            viewModel.UseSystemAccount = true;

            return viewModel;
        }

        public static ServiceControlEditViewModel Select_local_service_account(this ServiceControlEditViewModel viewModel)
        {
            viewModel.UseServiceAccount = true;

            return viewModel;
        }

        public static ServiceControlEditViewModel And_user_password_is(this ServiceControlEditViewModel viewModel, string password)
        {
            viewModel.Password = password;

            return viewModel;
        }

        public static ServiceControlEditViewModel And_user_account_is(this ServiceControlEditViewModel viewModel, string userAccount)
        {
            viewModel.ServiceAccount = userAccount;

            return viewModel;
        }
    }

    public class EditErrorInstanceAccountTests
    {
        public static string ShouldBeFalse = "{0} should be false";

        public static string ShouldBeTrue = "{0} should be true";

        public static string ShouldBeEmpty = "{0} should be empty";


        [Test]
        public void Local_system_account_selected()
        {
            var changedProperties = new List<string>();

            var viewModel =
                Given_editing_an_error_instance()
                    .Collect_changed_properties(changedProperties)
                    .Select_local_system_account();

            nameof(viewModel.ServiceAccount).Was_notified_of_change(changedProperties);

            nameof(viewModel.PasswordEnabled).Was_notified_of_change(changedProperties);

            Assert.Multiple(() =>
            {
                Assert.That(viewModel.UseSystemAccount, Is.True);
                Assert.That(viewModel.UseServiceAccount, Is.False);
                Assert.That(viewModel.UseProvidedAccount, Is.False);
                Assert.That(viewModel.PasswordEnabled, Is.False);
                Assert.That(viewModel.Password, Is.Empty);
                Assert.That(viewModel.ServiceAccount, Is.EqualTo("LocalSystem"));
            });

        }

        [Test]
        public void Local_service_account_selected()
        {
            var changedProperties = new List<string>();

            var viewModel =
                Given_editing_an_error_instance()
                    .Collect_changed_properties(changedProperties)
                    .Select_local_service_account();

            nameof(viewModel.ServiceAccount).Was_notified_of_change(changedProperties);

            nameof(viewModel.PasswordEnabled).Was_notified_of_change(changedProperties);

            nameof(viewModel.Password).Was_notified_of_change(changedProperties);

            Assert.Multiple(() =>
            {
                Assert.That(viewModel.UseSystemAccount, Is.False);
                Assert.That(viewModel.UseServiceAccount, Is.True);
                Assert.That(viewModel.UseProvidedAccount, Is.False);
                Assert.That(viewModel.PasswordEnabled, Is.False);
                Assert.That(viewModel.Password, Is.Empty);
                Assert.That(viewModel.ServiceAccount, Is.EqualTo("LocalService"));
            });
        }

        [Test]
        public void User_account_selected()
        {
            var changedProperties = new List<string>();

            var viewModel =
                Given_editing_an_error_instance()
                    .Collect_changed_properties(changedProperties)
                    .Select_user_account();

            nameof(viewModel.ServiceAccount).Was_notified_of_change(changedProperties);

            nameof(viewModel.PasswordEnabled).Was_notified_of_change(changedProperties);

            nameof(viewModel.Password).Was_notified_of_change(changedProperties);

            Assert.Multiple(() =>
            {
                Assert.That(viewModel.UseSystemAccount, Is.False);
                Assert.That(viewModel.UseServiceAccount, Is.False);
                Assert.That(viewModel.UseProvidedAccount, Is.True);
                Assert.That(viewModel.ServiceAccount, Is.Null);
                Assert.That(viewModel.PasswordEnabled, Is.True);
                Assert.That(viewModel.Password, Is.Null);
            });
        }

        [TestCase("foo", null)]
        [TestCase("foo", "")]
        [TestCase("foo", "bar")]
        public void User_account_selected_and_user_account_entered(string userAccount, string userPassword)
        {
            var changedProperties = new List<string>();

            var viewModel =
                Given_editing_an_error_instance()
                    .Collect_changed_properties(changedProperties)
                    .Select_user_account()
                    .And_user_account_is(userAccount)
                    .And_user_password_is(userPassword);

            nameof(viewModel.ServiceAccount).Was_notified_of_change(changedProperties);

            nameof(viewModel.PasswordEnabled).Was_notified_of_change(changedProperties);

            nameof(viewModel.Password).Was_notified_of_change(changedProperties);

            Assert.Multiple(() =>
            {
                Assert.That(viewModel.UseSystemAccount, Is.False, () => string.Format(ShouldBeFalse, nameof(viewModel.UseSystemAccount)));
                Assert.That(viewModel.UseServiceAccount, Is.False, () => string.Format(ShouldBeFalse, nameof(viewModel.UseServiceAccount)));
                Assert.That(viewModel.UseProvidedAccount, Is.True, () => string.Format(ShouldBeTrue, nameof(viewModel.UseProvidedAccount)));
                Assert.That(viewModel.PasswordEnabled, Is.True, () => string.Format(ShouldBeTrue, nameof(viewModel.PasswordEnabled)));
                Assert.That(viewModel.ServiceAccount, Is.EqualTo(userAccount));
                Assert.That(viewModel.Password, Is.EqualTo(userPassword));
            });
        }

        [TestCase("foo", null)]
        [TestCase("foo", "")]
        [TestCase("foo", "bar")]
        public void User_account_entered_then_system_account_selected(string userAccount,
            string userPassword)
        {
            var changedProperties = new List<string>();

            var viewModel =
                Given_editing_an_error_instance()
                    .Collect_changed_properties(changedProperties)
                    .Select_user_account()
                    .And_user_account_is(userAccount)
                    .And_user_password_is(userPassword)
                    .Select_local_system_account();

            nameof(viewModel.ServiceAccount).Was_notified_of_change(changedProperties);

            nameof(viewModel.PasswordEnabled).Was_notified_of_change(changedProperties);

            nameof(viewModel.Password).Was_notified_of_change(changedProperties);

            Assert.Multiple(() =>
            {
                Assert.That(viewModel.UseSystemAccount, Is.True, () => string.Format(ShouldBeFalse, nameof(viewModel.UseSystemAccount)));
                Assert.That(viewModel.UseServiceAccount, Is.False, () => string.Format(ShouldBeTrue, nameof(viewModel.UseServiceAccount)));
                Assert.That(viewModel.UseProvidedAccount, Is.False, () => string.Format(ShouldBeFalse, nameof(viewModel.UseProvidedAccount)));
                Assert.That(viewModel.PasswordEnabled, Is.False, () => string.Format(ShouldBeFalse, nameof(viewModel.PasswordEnabled)));
                Assert.That(viewModel.ServiceAccount, Is.EqualTo("LocalSystem"));
                Assert.That(viewModel.Password, Is.Empty);
            });
        }


        [TestCase("foo", null)]
        [TestCase("foo", "")]
        [TestCase("foo", "bar")]
        public void User_account_entered_then_local_service_account_selected(string userAccount,
            string userPassword)
        {
            var changedProperties = new List<string>();

            var viewModel =
                Given_editing_an_error_instance()
                    .Collect_changed_properties(changedProperties)
                    .Select_user_account()
                    .And_user_account_is(userAccount)
                    .And_user_password_is(userPassword)
                    .Select_local_service_account();

            nameof(viewModel.ServiceAccount).Was_notified_of_change(changedProperties);

            nameof(viewModel.PasswordEnabled).Was_notified_of_change(changedProperties);

            nameof(viewModel.Password).Was_notified_of_change(changedProperties);

            Assert.Multiple(() =>
            {
                Assert.That(viewModel.UseSystemAccount, Is.False, () => string.Format(ShouldBeFalse, nameof(viewModel.UseSystemAccount)));
                Assert.That(viewModel.UseServiceAccount, Is.True, () => string.Format(ShouldBeTrue, nameof(viewModel.UseServiceAccount)));
                Assert.That(viewModel.UseProvidedAccount, Is.False, () => string.Format(ShouldBeFalse, nameof(viewModel.UseProvidedAccount)));
                Assert.That(viewModel.PasswordEnabled, Is.False, () => string.Format(ShouldBeFalse, nameof(viewModel.PasswordEnabled)));
                Assert.That(viewModel.ServiceAccount, Is.EqualTo("LocalService"));
                Assert.That(viewModel.Password, Is.Empty);
            });
        }
    }
}