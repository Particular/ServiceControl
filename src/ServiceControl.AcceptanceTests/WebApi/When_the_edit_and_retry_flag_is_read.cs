namespace ServiceControl.AcceptanceTests.WebApi
{
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceControl.MessageFailures.Api;

    class When_the_edit_and_retry_flag_is_read : AcceptanceTest
    {
        [TestCase(true)]
        [TestCase(false)]
        public async Task Should_agree_with_whether_the_edit_route_answers(bool editingAllowed)
        {
            SetSettings = settings => settings.AllowMessageEditing = editingAllowed;

            EditConfigurationModel config = null;
            HttpStatusCode editStatus = default;

            await Define<Context>()
                .Done(async _ =>
                {
                    config = await this.TryGet<EditConfigurationModel>("/api/edit/config");

                    using var edit = await HttpClient.PostAsync("/api/edit/does-not-exist",
                        JsonContent.Create(new EditMessageModel { MessageBody = "{}" }, options: SerializerOptions));

                    editStatus = edit.StatusCode;

                    return config != null;
                })
                .Run();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(config.Enabled, Is.EqualTo(editingAllowed),
                    "ServicePulse offers the Edit button on this flag alone");

                Assert.That(editStatus == HttpStatusCode.NotFound, Is.Not.EqualTo(editingAllowed),
                    "The flag has to agree with the route: offering Edit while the route refuses every edit is worse than not offering it");

                Assert.That(config.LockedHeaders, Does.Contain(Headers.MessageId),
                    "The page greys out the headers it is told not to let anyone change");
            }
        }

        class Context : ScenarioContext;
    }
}
