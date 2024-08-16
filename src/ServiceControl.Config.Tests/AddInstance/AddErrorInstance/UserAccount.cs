namespace ServiceControl.Config.Tests.AddInstance.AddErrorInstance
{
    using System.Collections.Generic;
    using NUnit.Framework;
    using UI.InstanceAdd;
    using static ServiceControlAddViewModelExtensions;

    public static class ServiceControlAddViewModelExtensions
    {
        public static ServiceControlAddViewModel Given_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel();

            return viewModel;
        }

        public static ServiceControlAddViewModel When_user_account_selected(this ServiceControlAddViewModel viewModel)
        {
            viewModel.ErrorUseProvidedAccount = true;

            return viewModel;
        }

        public static ServiceControlAddViewModel When_local_system_account_selected(this ServiceControlAddViewModel viewModel)
        {
            viewModel.ErrorUseSystemAccount = true;

            return viewModel;
        }

        public static ServiceControlAddViewModel When_local_service_account_selected(this ServiceControlAddViewModel viewModel)
        {
            viewModel.ErrorUseServiceAccount = true;

            return viewModel;
        }

        public static ServiceControlAddViewModel And_user_password_is(this ServiceControlAddViewModel viewModel, string password)
        {
            viewModel.ErrorPassword = password;

            return viewModel;
        }

        public static ServiceControlAddViewModel And_user_account_is(this ServiceControlAddViewModel viewModel, string userAccount)
        {
            viewModel.ErrorServiceAccount = userAccount;

            return viewModel;
        }
    }

    public class AddErrorInstanceServiceAccountTests
    {
        public static string ShouldBeFalse = "{0} should be false";

        public static string ShouldBeTrue = "{0} should be true";

        public static string ShouldBeEmpty = "{0} should be empty";

        public static string ShouldEqual = "{0} should equal {1}";

        [Test]
        public void Screen_loaded()
        {
            var viewModel = Given_adding_error_instance();

            Assert.IsTrue(viewModel.ErrorUseSystemAccount);

            Assert.That(viewModel.ErrorUseServiceAccount, Is.False);

            Assert.That(viewModel.ErrorUseProvidedAccount, Is.False);

            Assert.That(viewModel.ErrorPasswordEnabled, Is.False);

            Assert.IsEmpty(viewModel.ErrorPassword);

            Assert.AreEqual(viewModel.ErrorServiceAccount, "LocalSystem");
        }

        [Test]
        public void Local_system_account_selected()
        {
            var changedProperties = new List<string>();

            var viewModel =
                Given_adding_error_instance()
                .Collect_changed_properties(changedProperties)
                .When_local_system_account_selected();

            Assert.IsTrue(viewModel.ErrorUseSystemAccount);

            Assert.That(viewModel.ErrorUseServiceAccount, Is.False);

            Assert.That(viewModel.ErrorUseProvidedAccount, Is.False);

            Assert.That(viewModel.ErrorPasswordEnabled, Is.False);

            Assert.IsEmpty(viewModel.ErrorPassword);

            Assert.AreEqual(viewModel.ErrorServiceAccount, "LocalSystem");
        }

        [Test]
        public void Local_service_account_selected()
        {
            var changedProperties = new List<string>();

            var viewModel =
                Given_adding_error_instance()
                    .Collect_changed_properties(changedProperties)
                .When_local_service_account_selected();

            nameof(viewModel.ErrorServiceAccount)
                .Was_notified_of_change(changedProperties);

            nameof(viewModel.ErrorPasswordEnabled)
                .Was_notified_of_change(changedProperties);

            Assert.That(viewModel.ErrorUseSystemAccount, Is.False);

            Assert.IsTrue(viewModel.ErrorUseServiceAccount);

            Assert.That(viewModel.ErrorUseProvidedAccount, Is.False);

            Assert.That(viewModel.ErrorPasswordEnabled, Is.False);

            Assert.IsEmpty(viewModel.ErrorPassword);

            Assert.AreEqual(viewModel.ErrorServiceAccount, "LocalService");
        }

        [Test]
        public void User_account_selected()
        {
            var changedProperties = new List<string>();

            var viewModel =
                Given_adding_error_instance()
                    .Collect_changed_properties(changedProperties)
                    .When_user_account_selected();

            nameof(viewModel.ErrorServiceAccount)
                .Was_notified_of_change(changedProperties);

            nameof(viewModel.ErrorPasswordEnabled)
                .Was_notified_of_change(changedProperties);

            Assert.That(viewModel.ErrorUseSystemAccount, Is.False);

            Assert.That(viewModel.ErrorUseServiceAccount, Is.False);

            Assert.IsTrue(viewModel.ErrorUseProvidedAccount);

            Assert.IsNull(viewModel.ErrorServiceAccount);

            Assert.IsTrue(viewModel.ErrorPasswordEnabled);

            Assert.IsNull(viewModel.ErrorPassword);
        }


        [TestCase("foo", null)]
        [TestCase("foo", "")]
        [TestCase("foo", "bar")]
        public void User_account_selected_and_user_account_entered(string userAccount, string userPassword)
        {
            var changedProperties = new List<string>();

            var viewModel =
                Given_adding_error_instance()
                    .Collect_changed_properties(changedProperties)
                    .When_user_account_selected()
                    .And_user_account_is(userAccount)
                    .And_user_password_is(userPassword);

            nameof(viewModel.ErrorServiceAccount)
                .Was_notified_of_change(changedProperties);

            nameof(viewModel.ErrorPasswordEnabled)
                .Was_notified_of_change(changedProperties);

            Assert.That(viewModel.ErrorUseSystemAccount, Is.False, () => string.Format(ShouldBeFalse, nameof(viewModel.ErrorUseSystemAccount)));

            Assert.That(viewModel.ErrorUseServiceAccount, Is.False, () => string.Format(ShouldBeFalse, nameof(viewModel.ErrorUseServiceAccount)));

            Assert.IsTrue(viewModel.ErrorUseProvidedAccount, ShouldBeTrue, nameof(viewModel.ErrorUseProvidedAccount));

            Assert.IsTrue(viewModel.ErrorPasswordEnabled, ShouldBeTrue, nameof(viewModel.ErrorPasswordEnabled));

            Assert.AreEqual(userAccount, viewModel.ErrorServiceAccount, ShouldEqual, nameof(viewModel.ErrorServiceAccount), userAccount);

            Assert.AreEqual(userPassword, viewModel.ErrorPassword, ShouldEqual, nameof(viewModel.ErrorPassword), userPassword);
        }

        [TestCase("foo", null)]
        [TestCase("foo", "")]
        [TestCase("foo", "bar")]
        public void User_account_entered_then_system_account_selected(string userAccount,
          string userPassword)
        {
            var changedProperties = new List<string>();

            var viewModel =
                Given_adding_error_instance()
                    .Collect_changed_properties(changedProperties)
                    .When_user_account_selected()
                    .And_user_account_is(userAccount)
                    .And_user_password_is(userPassword)
                    .When_local_system_account_selected();

            nameof(viewModel.ErrorServiceAccount).Was_notified_of_change(changedProperties);

            nameof(viewModel.ErrorPasswordEnabled).Was_notified_of_change(changedProperties);

            nameof(viewModel.ErrorPassword).Was_notified_of_change(changedProperties);

            Assert.IsTrue(viewModel.ErrorUseSystemAccount, ShouldBeFalse, nameof(viewModel.ErrorUseSystemAccount));

            Assert.That(viewModel.ErrorUseServiceAccount, Is.False, () => string.Format(ShouldBeTrue, nameof(viewModel.ErrorUseServiceAccount)));

            Assert.That(viewModel.ErrorUseProvidedAccount, Is.False, () => string.Format(ShouldBeFalse, nameof(viewModel.ErrorUseProvidedAccount)));

            Assert.That(viewModel.ErrorPasswordEnabled, Is.False, () => string.Format(ShouldBeFalse, nameof(viewModel.ErrorPasswordEnabled)));

            Assert.AreEqual(viewModel.ErrorServiceAccount, "LocalSystem");

            Assert.IsEmpty(viewModel.ErrorPassword);
        }


        [TestCase("foo", null)]
        [TestCase("foo", "")]
        [TestCase("foo", "bar")]
        public void User_account_entered_then_local_service_account_selected(string userAccount,
            string userPassword)
        {
            var changedProperties = new List<string>();

            var viewModel =
                Given_adding_error_instance()
                    .Collect_changed_properties(changedProperties)
                    .When_user_account_selected()
                    .And_user_account_is(userAccount)
                    .And_user_password_is(userPassword)
                    .When_local_service_account_selected();

            nameof(viewModel.ErrorServiceAccount).Was_notified_of_change(changedProperties);

            nameof(viewModel.ErrorPasswordEnabled).Was_notified_of_change(changedProperties);

            nameof(viewModel.ErrorPassword).Was_notified_of_change(changedProperties);

            Assert.That(viewModel.ErrorUseSystemAccount, Is.False, () => string.Format(ShouldBeFalse, nameof(viewModel.ErrorUseSystemAccount)));

            Assert.IsTrue(viewModel.ErrorUseServiceAccount, ShouldBeTrue, nameof(viewModel.ErrorUseServiceAccount));

            Assert.That(viewModel.ErrorUseProvidedAccount, Is.False, () => string.Format(ShouldBeFalse, nameof(viewModel.ErrorUseProvidedAccount)));

            Assert.That(viewModel.ErrorPasswordEnabled, Is.False, () => string.Format(ShouldBeFalse, nameof(viewModel.ErrorPasswordEnabled)));

            Assert.AreEqual(viewModel.ErrorServiceAccount, "LocalService");

            Assert.IsEmpty(viewModel.ErrorPassword);
        }
    }
}
