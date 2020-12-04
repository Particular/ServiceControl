namespace ServiceControl.UnitTests.Recoverability
{
    using System.Collections.Generic;
    using NUnit.Framework;
    using Particular.Approvals;
    using ServiceControl.Recoverability;

    [TestFixture]
    public class HeaderFilterTests
    {
        [Test]
        public void RemovesHeaders()
        {
            var headers = new Dictionary<string, string>
            {
                { "NServiceBus.MessageId", "caf68027-acce-4260-8442-ac6500e46afc" },
                { "NServiceBus.MessageIntent", "Publish" },
                { "NServiceBus.RelatedTo", "2e6c6c9c-c4cb-474e-98b8-ac6500e46a0c" },
                { "NServiceBus.ConversationId", "37c405c8-e4e4-4873-8eb4-ac6500e46621" },
                { "NServiceBus.CorrelationId", "6e449174-6f77-4da4-b9c0-ac6500e46621" },
                { "NServiceBus.OriginatingMachine", "MACHINE"},
                { "NServiceBus.OriginatingEndpoint", "PurchaseOrderService.1.0" },
                { "$.diagnostics.originating.hostid", "4f8138bdb0421ffe1ceaee86e9145721" },
                { "NServiceBus.OriginatingSagaId", "9e0d2f01-e903-481a-b272-ac6500e46715" },
                { "NServiceBus.OriginatingSagaType", "PowerSupplyPurchaseOrderService.PurchaseOrderSaga, PowerSupplyPurchaseOrderService, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null" },
                { "NServiceBus.ReplyToAddress", "PowerSupplyPurchaseOrderService.1.0@[dbo]@[Market.NServiceBus.Prod]" },
                { "NServiceBus.ContentType", "application/json" },
                { "NServiceBus.EnclosedMessageTypes", "PowerSupplyPurchaseOrderService.ApiModels.Events.v1_0.PowerSupplyDebtorBlacklistCheckCompleted, PowerSupplyOrderService.ApiModels, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null" },
                { "NServiceBus.Version", "7.1.0" },
                { "NServiceBus.TimeSent", "2020-10-31 13:59:26:479745 Z" },
                { "NServiceBus.Retries.Timestamp", "2020-10-31 13:56:55:359877 Z" },
                { "NServiceBus.Timeout.RouteExpiredTimeoutTo", "Crm.PowerSupplySalesOrderManager.1.0@[dbo]@[Market.NServiceBus.Prod]" },
                { "NServiceBus.Timeout.Expire", "2020-10-31 13,59,25,359877 Z" },
                { "NServiceBus.RelatedToTimeoutId", "2c6aa21f-7e73-4142-de86-08d87432ffe4" },
                { "NServiceBus.ExceptionInfo.Data.Message type", "PowerSupplyPurchaseOrderService.ApiModels.Events.v1_0.PowerSupplyDebtorBlacklistCheckCompleted" },
                { "NServiceBus.ExceptionInfo.Data.Handler type", "Crm.PowerSupplySalesOrderManager.MessageHandlers.DebtorBlacklistCheckCompletedHandler" },
                { "NServiceBus.ExceptionInfo.Data.Handler start time", "31-10-2020 13:59:28" },
                { "NServiceBus.ExceptionInfo.Data.Handler failure time", "31-10-2020 13:59:28" },
                { "NServiceBus.ExceptionInfo.Data.Message ID", "caf68027-acce-4260-8442-ac6500e46afc" },
                { "NServiceBus.ExceptionInfo.Data.Transport message ID", "320fb8cb-ad20-48e9-a111-454ebe43a7a8" },
                { "NServiceBus.ExceptionInfo.Data.Custom Entry", "Custom" },
                { "NServiceBus.ProcessingMachine", "MACHINE" },
                { "NServiceBus.ProcessingEndpoint", "SeasNve.Market.Crm.PowerSupplySalesOrderManager.1.0" },
                { "$.diagnostics.hostid", "8d8fcac767fbd7199024c5cae57adde5" },
                { "$.diagnostics.hostdisplayname", "MACHINE" },
                { "ServiceControl.Retry.UniqueMessageId", "a5f6da09-5f3f-5394-c09c-dffbe99c357a" },
                { "NServiceBus.ProcessingStarted", "2020-11-02 08:07:44:650218 Z" },
                { "NServiceBus.ProcessingEnded", "2020-11-02 08:07:44:837731 Z" }
            };

            var headersToRetryWith = HeaderFilter.RemoveErrorMessageHeaders(headers);

            Approver.Verify(headersToRetryWith);
        }

    }
}