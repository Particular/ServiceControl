﻿namespace ServiceControl.MessageFailures.Api
{
    using System;
    using System.Collections.Generic;
    using InternalMessages;
    using Nancy;
    using Nancy.ModelBinding;
    using NServiceBus;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class RetryMessages : BaseModule
    {
        public RetryMessages()
        {

            Post["/errors/{messageid}/retry"] = parameters =>
            {
                var failedMessageId = parameters.MessageId;

                Bus.SendLocal<RequestRetry>(m =>
                {
                    m.SetHeader("RequestedAt", DateTimeExtensions.ToWireFormattedString(DateTime.UtcNow));
                    m.FailedMessageId = failedMessageId;
                });

                return HttpStatusCode.Accepted;
            };

            Post["/errors/retry"] = _ =>
            {
                var ids = this.Bind<List<string>>();

                var requestedAt = DateTimeExtensions.ToWireFormattedString(DateTime.UtcNow);

                foreach (var id in ids)
                {
                    var request = new RequestRetry { FailedMessageId = id };

                    request.SetHeader("RequestedAt", requestedAt);

                    Bus.SendLocal(request);    
                }

                

                return HttpStatusCode.Accepted;
            };

            Post["/errors/retry/all"] = _ =>
            {
                var request = new RequestRetryAll();
                request.SetHeader("RequestedAt", DateTimeExtensions.ToWireFormattedString(DateTime.UtcNow));

                Bus.SendLocal(request);

                return HttpStatusCode.Accepted;
            };

            Post["/errors/{name}/retry/all"] = parameters =>
            {
                var request = new RequestRetryAll { Endpoint = parameters.name };
                request.SetHeader("RequestedAt", DateTimeExtensions.ToWireFormattedString(DateTime.UtcNow));

                Bus.SendLocal(request);

                return HttpStatusCode.Accepted;
            };
        }

        public IBus Bus { get; set; }
    }


}