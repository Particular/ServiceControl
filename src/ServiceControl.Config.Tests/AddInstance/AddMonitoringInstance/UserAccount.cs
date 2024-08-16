namespace ServiceControl.Config.Tests.AddInstance.AddMonitoringInstance
{
    using System.Collections.Generic;
    using NUnit.Framework;
    using ServiceControl.Config.UI.InstanceAdd;
    using static MonitoringAddViewModelExtensions;

    public static class MonitoringAddViewModelExtensions
    {
        public static MonitoringAddViewModel Given_adding_monitoring_instance()
        {
            var viewModel = new MonitoringAddViewModel()
            {
                //This is required to notify the property change in the tests. 
                //When its figured out how to pass an instance to the constructor this will not be needed

                UseServiceAccount = false,

                UseSystemAccount = true,

                UseProvidedAccount = false
            };

            return viewModel;
        }

        public static MonitoringAddViewModel When_user_account_selected(this MonitoringAddViewModel viewModel)
        {
            viewModel.UseProvidedAccount = true;
            return viewModel;
        }

        public static MonitoringAddViewModel When_local_system_account_selected(this MonitoringAddViewModel viewModel)
        {
            viewModel.UseSystemAccount = true;
            return viewModel;
        }

        public static MonitoringAddViewModel When_local_service_account_selected(this MonitoringAddViewModel viewModel)
        {
            viewModel.UseServiceAccount = true;
            return viewModel;
        }

        public static MonitoringAddViewModel And_user_password_is(this MonitoringAddViewModel viewModel, string password)
        {
            viewModel.Password = password;

            return viewModel;
        }

        public static MonitoringAddViewModel And_user_account_is(this MonitoringAddViewModel viewModel, string userAccount)
        {
            viewModel.ServiceAccount = userAccount;

            return viewModel;
        }
    }

    public class AddMonitoringServiceAccountTests
    {
        public static string ShouldBeFalse = "{0} should be false";

        public static string ShouldBeTrue = "{0} should be true";

        public static string ShouldBeEmpty = "{0} should be empty";

        public static string ShouldEqual = "{0} should equal {1}";

        [Test]
        public void Screen_loaded()
        {
            var viewModel = Given_adding_monitoring_instance();

            Assert.IsTrue(viewModel.UseSystemAccount);

            Assert.That(viewModel.UseServiceAccount, Is.False);

            Assert.That(viewModel.UseProvidedAccount, Is.False);

            Assert.That(viewModel.PasswordEnabled, Is.False);

            Assert.IsEmpty(viewModel.Password);

            Assert.AreEqual(viewModel.ServiceAccount, "LocalSystem");
        }

        [Test]
        public void Local_system_account_selected()
        {
            var changedProperties = new List<string>();

            var viewModel =
                Given_adding_monitoring_instance()
                .Collect_changed_properties(changedProperties)
                .When_local_system_account_selected();

            Assert.IsTrue(viewModel.UseSystemAccount);

            Assert.That(viewModel.UseServiceAccount, Is.False);

            Assert.That(viewModel.UseProvidedAccount, Is.False);

            Assert.That(viewModel.PasswordEnabled, Is.False);

            Assert.IsEmpty(viewModel.Password);

            Assert.AreEqual(viewModel.ServiceAccount, "LocalSystem");
        }

        [Test]
        public void Local_service_account_selected()
        {
            var changedProperties = new List<string>();

            var viewModel =
                Given_adding_monitoring_instance()
                    .Collect_changed_properties(changedProperties)
                    .When_local_service_account_selected();

            nameof(viewModel.ServiceAccount)
                .Was_notified_of_change(changedProperties);

            nameof(viewModel.PasswordEnabled)
                .Was_notified_of_change(changedProperties);

            Assert.That(viewModel.UseSystemAccount, Is.False);

            Assert.IsTrue(viewModel.UseServiceAccount);

            Assert.That(viewModel.UseProvidedAccount, Is.False);

            Assert.That(viewModel.PasswordEnabled, Is.False);

            Assert.IsEmpty(viewModel.Password);

            Assert.AreEqual(viewModel.ServiceAccount, "LocalService");
        }

        [Test]
        public void User_account_selected()
        {
            var changedProperties = new List<string>();

            var viewModel =
                Given_adding_monitoring_instance()
                    .Collect_changed_properties(changedProperties)
                    .When_user_account_selected();

            nameof(viewModel.ServiceAccount)
                .Was_notified_of_change(changedProperties);

            nameof(viewModel.PasswordEnabled)
                .Was_notified_of_change(changedProperties);

            Assert.That(viewModel.UseSystemAccount, Is.False);

            Assert.That(viewModel.UseServiceAccount, Is.False);

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
                Given_adding_monitoring_instance()
                    .Collect_changed_properties(changedProperties)
                    .When_user_account_selected()
                    .And_user_account_is(userAccount)
                    .And_user_password_is(userPassword);

            nameof(viewModel.ServiceAccount)
                .Was_notified_of_change(changedProperties);

            nameof(viewModel.PasswordEnabled)
                .Was_notified_of_change(changedProperties);

            Assert.That(viewModel.UseSystemAccount, Is.False, () => string.Format(ShouldBeFalse, nameof(viewModel.UseSystemAccount)));

            Assert.That(viewModel.UseServiceAccount, Is.False, () => string.Format(ShouldBeFalse, nameof(viewModel.UseServiceAccount)));

            Assert.IsTrue(viewModel.UseProvidedAccount, ShouldBeTrue, nameof(viewModel.UseProvidedAccount));

            Assert.IsTrue(viewModel.PasswordEnabled, ShouldBeTrue, nameof(viewModel.PasswordEnabled));

            Assert.AreEqual(userAccount, viewModel.ServiceAccount, ShouldEqual, nameof(viewModel.ServiceAccount), userAccount);

            Assert.AreEqual(userPassword, viewModel.Password, ShouldEqual, nameof(viewModel.Password), userPassword);
        }

        [TestCase("foo", null)]
        [TestCase("foo", "")]
        [TestCase("foo", "bar")]
        public void User_account_entered_then_system_account_selected(string userAccount,
          string userPassword)
        {
            var changedProperties = new List<string>();

            var viewModel =
                Given_adding_monitoring_instance()
                    .Collect_changed_properties(changedProperties)
                    .When_user_account_selected()
                    .And_user_account_is(userAccount)
                    .And_user_password_is(userPassword)
                    .When_local_system_account_selected();

            nameof(viewModel.ServiceAccount).Was_notified_of_change(changedProperties);

            nameof(viewModel.PasswordEnabled).Was_notified_of_change(changedProperties);

            nameof(viewModel.Password).Was_notified_of_change(changedProperties);

            Assert.IsTrue(viewModel.UseSystemAccount, ShouldBeFalse, nameof(viewModel.UseSystemAccount));

            Assert.That(viewModel.UseServiceAccount, Is.False, () => string.Format(ShouldBeTrue, nameof(viewModel.UseServiceAccount)));

            Assert.That(viewModel.UseProvidedAccount, Is.False, () => string.Format(ShouldBeFalse, nameof(viewModel.UseProvidedAccount)));

            Assert.That(viewModel.PasswordEnabled, Is.False, () => string.Format(ShouldBeFalse, nameof(viewModel.PasswordEnabled)));

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
                Given_adding_monitoring_instance()
                    .Collect_changed_properties(changedProperties)
                    .When_user_account_selected()
                    .And_user_account_is(userAccount)
                    .And_user_password_is(userPassword)
                    .When_local_service_account_selected();

            nameof(viewModel.ServiceAccount).Was_notified_of_change(changedProperties);

            nameof(viewModel.PasswordEnabled).Was_notified_of_change(changedProperties);

            nameof(viewModel.Password).Was_notified_of_change(changedProperties);

            Assert.That(viewModel.UseSystemAccount, Is.False, () => string.Format(ShouldBeFalse, nameof(viewModel.UseSystemAccount)));

            Assert.IsTrue(viewModel.UseServiceAccount, ShouldBeTrue, nameof(viewModel.UseServiceAccount));

            Assert.That(viewModel.UseProvidedAccount, Is.False, () => string.Format(ShouldBeFalse, nameof(viewModel.UseProvidedAccount)));

            Assert.That(viewModel.PasswordEnabled, Is.False, () => string.Format(ShouldBeFalse, nameof(viewModel.PasswordEnabled)));

            Assert.AreEqual(viewModel.ServiceAccount, "LocalService");

            Assert.IsEmpty(viewModel.Password);
        }
    }
}
