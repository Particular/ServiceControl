﻿namespace ServiceControl.Config.Tests.AddInstance.AddAuditInstance
{
    using System.Collections.Generic;
    using NUnit.Framework;
    using UI.InstanceAdd;
    using static ServiceControlAddAuditViewModelExtensions;

    public static class ServiceControlAddAuditViewModelExtensions
    {
        public static ServiceControlAddViewModel Given_adding_audit_instance()
        {
            var viewModel = new ServiceControlAddViewModel();

            return viewModel;
        }

        public static ServiceControlAddViewModel When_user_account_selected(this ServiceControlAddViewModel viewModel)
        {
            viewModel.AuditUseProvidedAccount = true;

            return viewModel;
        }

        public static ServiceControlAddViewModel When_local_system_selected(this ServiceControlAddViewModel viewModel)
        {
            viewModel.AuditUseSystemAccount = true;

            return viewModel;
        }

        public static ServiceControlAddViewModel When_local_service_selected(this ServiceControlAddViewModel viewModel)
        {
            viewModel.AuditUseServiceAccount = true;

            return viewModel;
        }

        public static ServiceControlAddViewModel And_user_password_is(this ServiceControlAddViewModel viewModel, string password)
        {
            viewModel.AuditPassword = password;

            return viewModel;
        }

        public static ServiceControlAddViewModel And_user_account_is(this ServiceControlAddViewModel viewModel, string userAccount)
        {
            viewModel.AuditServiceAccount = userAccount;

            return viewModel;
        }
    }

    public class AddAuditInstanceServiceAccountTests
    {
        public static string ShouldBeFalse = "{0} should be false";

        public static string ShouldBeTrue = "{0} should be true";

        public static string ShouldBeEmpty = "{0} should be empty";

        [Test]
        public void Screen_loaded()
        {
            var viewModel =
                Given_adding_audit_instance();

            Assert.IsTrue(viewModel.AuditUseSystemAccount);

            Assert.IsFalse(viewModel.AuditUseServiceAccount);

            Assert.IsFalse(viewModel.AuditUseProvidedAccount);

            Assert.IsFalse(viewModel.AuditPasswordEnabled);

            Assert.IsEmpty(viewModel.AuditPassword);

            Assert.AreEqual(viewModel.AuditServiceAccount, "LocalSystem");
        }

        [Test]
        public void Local_system_account_selected()
        {
            var viewModel =
                Given_adding_audit_instance()
                    .When_local_system_selected();

            Assert.IsTrue(viewModel.AuditUseSystemAccount);

            Assert.IsFalse(viewModel.AuditUseServiceAccount);

            Assert.IsFalse(viewModel.AuditUseProvidedAccount);

            Assert.IsFalse(viewModel.AuditPasswordEnabled);

            Assert.IsEmpty(viewModel.AuditPassword);

            Assert.AreEqual(viewModel.AuditServiceAccount, "LocalSystem");

        }

        [Test]
        public void Local_service_account_selected()
        {
            var viewModel =
                Given_adding_audit_instance()
                    .When_local_service_selected();

            Assert.IsFalse(viewModel.AuditUseSystemAccount);

            Assert.IsTrue(viewModel.AuditUseServiceAccount);

            Assert.IsFalse(viewModel.AuditUseProvidedAccount);

            Assert.IsFalse(viewModel.AuditPasswordEnabled);

            Assert.IsEmpty(viewModel.AuditPassword);

            Assert.AreEqual(viewModel.AuditServiceAccount, "LocalService");
        }

        [Test]
        public void User_account_selected()
        {
            var changedProperties = new List<string>();

            var viewModel =
                Given_adding_audit_instance()
                    .Collect_changed_properties(changedProperties)
                    .When_user_account_selected();

            nameof(viewModel.AuditServiceAccount).Was_notified_of_change(changedProperties);

            nameof(viewModel.AuditPasswordEnabled).Was_notified_of_change(changedProperties);

            Assert.IsFalse(viewModel.AuditUseSystemAccount);

            Assert.IsFalse(viewModel.AuditUseServiceAccount);

            Assert.IsTrue(viewModel.AuditUseProvidedAccount);

            Assert.IsNull(viewModel.AuditServiceAccount);

            Assert.IsTrue(viewModel.AuditPasswordEnabled);

            Assert.IsNull(viewModel.AuditPassword);
        }

        [TestCase("foo", null)]
        [TestCase("foo", "")]
        [TestCase("foo", "bar")]
        public void User_account_selected_and_user_account_entered(string userAccount, string userPassword)
        {
            var changedProperties = new List<string>();

            var viewModel =
                Given_adding_audit_instance()
                    .Collect_changed_properties(changedProperties)
                    .When_user_account_selected()
                    .And_user_account_is(userAccount)
                    .And_user_password_is(userPassword);

            nameof(viewModel.AuditServiceAccount).Was_notified_of_change(changedProperties);

            nameof(viewModel.AuditPasswordEnabled).Was_notified_of_change(changedProperties);

            Assert.IsFalse(viewModel.AuditUseSystemAccount, ShouldBeFalse, nameof(viewModel.AuditUseSystemAccount));

            Assert.IsFalse(viewModel.AuditUseServiceAccount, ShouldBeFalse, nameof(viewModel.AuditUseServiceAccount));

            Assert.IsTrue(viewModel.AuditUseProvidedAccount, ShouldBeTrue, nameof(viewModel.AuditUseProvidedAccount));

            Assert.IsTrue(viewModel.AuditPasswordEnabled, ShouldBeTrue, nameof(viewModel.AuditPasswordEnabled));

            Assert.AreEqual(userAccount, viewModel.AuditServiceAccount);

            Assert.AreEqual(userPassword, viewModel.AuditPassword);
        }

        [TestCase("foo", null)]
        [TestCase("foo", "")]
        [TestCase("foo", "bar")]
        public void User_account_entered_then_system_account_selected(string userAccount,
          string userPassword)
        {
            var changedProperties = new List<string>();

            var viewModel =
                Given_adding_audit_instance()
                    .Collect_changed_properties(changedProperties)
                    .When_user_account_selected()
                    .And_user_account_is(userAccount)
                    .And_user_password_is(userPassword)
                    .When_local_system_selected();

            nameof(viewModel.AuditServiceAccount).Was_notified_of_change(changedProperties);

            nameof(viewModel.AuditPasswordEnabled).Was_notified_of_change(changedProperties);

            nameof(viewModel.AuditPassword).Was_notified_of_change(changedProperties);

            Assert.IsTrue(viewModel.AuditUseSystemAccount, ShouldBeFalse, nameof(viewModel.AuditUseSystemAccount));

            Assert.IsFalse(viewModel.AuditUseServiceAccount, ShouldBeTrue, nameof(viewModel.AuditUseServiceAccount));

            Assert.IsFalse(viewModel.AuditUseProvidedAccount, ShouldBeFalse, nameof(viewModel.AuditUseProvidedAccount));

            Assert.IsFalse(viewModel.AuditPasswordEnabled, ShouldBeFalse, nameof(viewModel.AuditPasswordEnabled));

            Assert.AreEqual(viewModel.AuditServiceAccount, "LocalSystem");

            Assert.IsEmpty(viewModel.AuditPassword);
        }


        [TestCase("foo", null)]
        [TestCase("foo", "")]
        [TestCase("foo", "bar")]
        public void User_account_entered_then_local_service_account_selected(string userAccount,
            string userPassword)
        {
            var changedProperties = new List<string>();

            var viewModel =
                Given_adding_audit_instance()
                    .Collect_changed_properties(changedProperties)
                    .When_user_account_selected()
                    .And_user_account_is(userAccount)
                    .And_user_password_is(userPassword)
                    .When_local_service_selected();

            nameof(viewModel.AuditServiceAccount).Was_notified_of_change(changedProperties);

            nameof(viewModel.AuditPasswordEnabled).Was_notified_of_change(changedProperties);

            nameof(viewModel.AuditPassword).Was_notified_of_change(changedProperties);

            Assert.IsFalse(viewModel.AuditUseSystemAccount, ShouldBeFalse, nameof(viewModel.AuditUseSystemAccount));

            Assert.IsTrue(viewModel.AuditUseServiceAccount, ShouldBeTrue, nameof(viewModel.AuditUseServiceAccount));

            Assert.IsFalse(viewModel.AuditUseProvidedAccount, ShouldBeFalse, nameof(viewModel.AuditUseProvidedAccount));

            Assert.IsFalse(viewModel.AuditPasswordEnabled, ShouldBeFalse, nameof(viewModel.AuditPasswordEnabled));

            Assert.AreEqual(viewModel.AuditServiceAccount, "LocalService");

            Assert.IsEmpty(viewModel.AuditPassword);
        }
    }
}
