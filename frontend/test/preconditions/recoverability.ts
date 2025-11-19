import Configuration, { EditAndRetryConfig } from "@/resources/Configuration";
import { SetupFactoryOptions } from "../driver";
import RecoverabilityHistoryResponse from "@/resources/RecoverabilityHistoryResponse";
import { FailedMessage } from "@/resources/FailedMessage";
import Message from "@/resources/Message";

export const serviceControlConfigurationDefaultHandler = ({ driver }: SetupFactoryOptions) => {
  const serviceControlUrl = window.defaultConfig.service_control_url;
  driver.mockEndpoint(`${serviceControlUrl}configuration`, {
    body: <Configuration>{
      host: {
        service_name: "Particular.ServiceControl",
        raven_db_path: "",
        logging: {
          log_path: "",
          logging_level: "Info",
          raven_db_log_level: "Info",
        },
      },
      data_retention: {
        error_retention_period: "30.00:00:00",
      },
      performance_tunning: {
        http_default_connection_limit: 100,
        external_integrations_dispatching_batch_size: 100,
        expiration_process_batch_size: 100,
        expiration_process_timer_in_seconds: 300,
      },
      transport: {
        transport_type: "MSMQ",
        error_log_queue: "error.log",
        error_queue: "error",
        forward_error_messages: true,
      },
      plugins: {
        heartbeat_grace_period: "00:00:00",
      },
    },
  });
};

export const archivedGroupsWithClassifierDefaulthandler = ({ driver }: SetupFactoryOptions) => {
  const serviceControlUrl = window.defaultConfig.service_control_url;
  driver.mockEndpoint(`${serviceControlUrl}errors/groups{/:classifier}`, {
    body: [],
  });
};

export const recoverabilityGroupsWithClassifierDefaulthandler = ({ driver }: SetupFactoryOptions) => {
  const serviceControlUrl = window.defaultConfig.service_control_url;
  driver.mockEndpoint(`${serviceControlUrl}recoverability/groups{/:classifier}`, {
    body: [],
  });
};

export const recoverabilityClassifiers = ({ driver }: SetupFactoryOptions) => {
  const serviceControlUrl = window.defaultConfig.service_control_url;
  driver.mockEndpoint(`${serviceControlUrl}recoverability/classifiers`, {
    body: <string[]>["Exception Type and Stack Trace", "Message Type", "Endpoint Address", "Endpoint Instance", "Endpoint Name"],
  });
};

export const recoverabilityHistoryDefaultHandler = ({ driver }: SetupFactoryOptions) => {
  const serviceControlUrl = window.defaultConfig.service_control_url;
  driver.mockEndpoint(`${serviceControlUrl}recoverability/history`, {
    body: <RecoverabilityHistoryResponse>{
      id: "RetryOperations/History",
      historic_operations: [],
      unacknowledged_operations: [],
    },
  });
};

const editConfig = <EditAndRetryConfig>{
  enabled: false,
  sensitive_headers: [
    "NServiceBus.Header.RouteTo",
    "NServiceBus.DestinationSites",
    "NServiceBus.OriginatingSite",
    "NServiceBus.To",
    "NServiceBus.ReplyToAddress",
    "NServiceBus.ReturnMessage.ErrorCode",
    "NServiceBus.SagaType",
    "NServiceBus.OriginatingSagaType",
    "NServiceBus.TimeSent",
    "Header",
  ],
  locked_headers: [
    "NServiceBus.MessageId",
    "NServiceBus.SagaId",
    "NServiceBus.CorrelationId",
    "NServiceBus.ControlMessage",
    "NServiceBus.OriginatingSagaId",
    "NServiceBus.RelatedTo",
    "NServiceBus.ConversationId",
    "NServiceBus.MessageIntent",
    "NServiceBus.Version",
    "NServiceBus.IsSagaTimeoutMessage",
    "NServiceBus.IsDeferredMessage",
    "NServiceBus.Retries",
    "NServiceBus.Retries.Timestamp",
    "NServiceBus.FLRetries",
    "NServiceBus.ProcessingStarted",
    "NServiceBus.ProcessingEnded",
    "NServiceBus.ExceptionInfo.ExceptionType",
    "NServiceBus.ExceptionInfo.HelpLink",
    "NServiceBus.ExceptionInfo.Message",
    "NServiceBus.ExceptionInfo.Source",
    "NServiceBus.ExceptionInfo.StackTrace",
    "NServiceBus.TimeOfFailure",
    "NServiceBus.FailedQ",
  ],
};
export const recoverabilityEditConfigDefaultHandler = ({ driver }: SetupFactoryOptions) => {
  const serviceControlUrl = window.defaultConfig.service_control_url;
  driver.mockEndpoint(`${serviceControlUrl}edit/config`, {
    body: editConfig,
  });
};

export const enableEditAndRetry = ({ driver }: SetupFactoryOptions) => {
  const serviceControlUrl = window.defaultConfig.service_control_url;
  const config = structuredClone(editConfig);
  config.enabled = true;
  driver.mockEndpoint(`${serviceControlUrl}edit/config`, {
    body: config,
  });
};

export const hasFailedMessage =
  ({ withGroupId, withMessageId, withContentType, withBody }: { withGroupId: string; withMessageId: string; withContentType: string; withBody: Record<string, string | number | boolean> | string | number | boolean | null | undefined }) =>
  ({ driver }: SetupFactoryOptions) => {
    const serviceControlUrl = window.defaultConfig.service_control_url;

    const failedMessage = <FailedMessage>{
      id: withGroupId,
      message_type: "ServiceControl.SmokeTest.SimpleCommand",
      time_sent: "2024-06-27T06:14:48.895733Z",
      is_system_message: false,
      exception: {
        exception_type: "System.Exception",
        message: "SimpleHandler: Throwing exceptions enabled in Endpoint1",
        source: "ServiceControl.SmokeTest",
        stack_trace:
          "System.Exception: SimpleHandler: Throwing exceptions enabled in Endpoint1\r\n   at ServiceControl.SmokeTest.SimpleHandler.Handle(SimpleCommand message, IMessageHandlerContext context) in /_/src/ServiceControl.SmokeTest/SimpleHandler.cs:line 26\r\n   at NServiceBus.InvokeHandlerTerminator.Terminate(IInvokeHandlerContext context) in /_/src/NServiceBus.Core/Pipeline/Incoming/InvokeHandlerTerminator.cs:line 33\r\n   at NServiceBus.SagaAudit.AuditInvokedSagaBehavior.Invoke(IInvokeHandlerContext context, Func`1 next) in /_/src/NServiceBus.SagaAudit/AuditInvokedSagaBehavior.cs:line 13\r\n   at NServiceBus.SagaPersistenceBehavior.Invoke(IInvokeHandlerContext context, Func`2 next) in /_/src/NServiceBus.Core/Sagas/SagaPersistenceBehavior.cs:line 41\r\n   at NServiceBus.SagaAudit.CaptureSagaStateBehavior.Invoke(IInvokeHandlerContext context, Func`1 next) in /_/src/NServiceBus.SagaAudit/CaptureSagaStateBehavior.cs:line 33\r\n   at NServiceBus.LoadHandlersConnector.Invoke(IIncomingLogicalMessageContext context, Func`2 stage) in /_/src/NServiceBus.Core/Pipeline/Incoming/LoadHandlersConnector.cs:line 44\r\n   at NServiceBus.InvokeSagaNotFoundBehavior.Invoke(IIncomingLogicalMessageContext context, Func`2 next) in /_/src/NServiceBus.Core/Sagas/InvokeSagaNotFoundBehavior.cs:line 17\r\n   at NServiceBus.DeserializeMessageConnector.Invoke(IIncomingPhysicalMessageContext context, Func`2 stage) in /_/src/NServiceBus.Core/Pipeline/Incoming/DeserializeMessageConnector.cs:line 32\r\n   at ReceivePerformanceDiagnosticsBehavior.Invoke(IIncomingPhysicalMessageContext context, Func`2 next) in /_/src/NServiceBus.Metrics/ProbeBuilders/ReceivePerformanceDiagnosticsBehavior.cs:line 18\r\n   at NServiceBus.InvokeAuditPipelineBehavior.Invoke(IIncomingPhysicalMessageContext context, Func`2 next) in /_/src/NServiceBus.Core/Audit/InvokeAuditPipelineBehavior.cs:line 19\r\n   at NServiceBus.ProcessingStatisticsBehavior.Invoke(IIncomingPhysicalMessageContext context, Func`2 next) in /_/src/NServiceBus.Core/Performance/Statistics/ProcessingStatisticsBehavior.cs:line 25\r\n   at NServiceBus.TransportReceiveToPhysicalMessageConnector.Invoke(ITransportReceiveContext context, Func`2 next) in /_/src/NServiceBus.Core/Pipeline/Incoming/TransportReceiveToPhysicalMessageConnector.cs:line 35\r\n   at NServiceBus.RetryAcknowledgementBehavior.Invoke(ITransportReceiveContext context, Func`2 next) in /_/src/NServiceBus.Core/ServicePlatform/Retries/RetryAcknowledgementBehavior.cs:line 25\r\n   at NServiceBus.MainPipelineExecutor.Invoke(MessageContext messageContext, CancellationToken cancellationToken) in /_/src/NServiceBus.Core/Pipeline/MainPipelineExecutor.cs:line 49\r\n   at NServiceBus.MainPipelineExecutor.Invoke(MessageContext messageContext, CancellationToken cancellationToken) in /_/src/NServiceBus.Core/Pipeline/MainPipelineExecutor.cs:line 68\r\n   at NServiceBus.LearningTransportMessagePump.ProcessFile(ILearningTransportTransaction transaction, String messageId, CancellationToken messageProcessingCancellationToken) in /_/src/NServiceBus.Core/Transports/Learning/LearningTransportMessagePump.cs:line 340",
      },
      message_id: withMessageId,
      number_of_processing_attempts: 1,
      status: "unresolved",
      sending_endpoint: { name: "Sender", host_id: "abb12931-1352-fd70-02c2-b78f6daab553", host: "mobvm2" },
      receiving_endpoint: { name: "Endpoint1", host_id: "abb12931-1352-fd70-02c2-b78f6daab553", host: "mobvm2" },
      queue_address: "Endpoint1",
      time_of_failure: "2024-06-27T06:14:48.912923Z",
      last_modified: "2024-06-27T06:14:49.2216249Z",
      edited: false,
      edit_of: "",
    };

    driver.mockEndpointDynamic(`${serviceControlUrl}errors`, "get", (url) => {
      const status = url.searchParams.get("status");
      if (status === "unresolved") {
        return Promise.resolve({
          body: [failedMessage],
          headers: { "Total-Count": "1" },
        });
      }

      //For status=archived or status=retryissued
      return Promise.resolve({
        body: [],
        headers: { "Total-Count": "0" },
      });
    });

    driver.mockEndpoint(`${serviceControlUrl}messages/${withMessageId}/body`, {
      body: withBody,
    });

    driver.mockEndpoint(`${serviceControlUrl}messages/search/${withMessageId}`, {
      body: [
        <Message>{
          id: withGroupId,
          message_id: withMessageId,
          message_type: "ServiceControl.SmokeTest.SimpleCommand",
          sending_endpoint: { name: "Sender", host_id: "abb12931-1352-fd70-02c2-b78f6daab553", host: "mobvm2" },
          receiving_endpoint: { name: "Endpoint1", host_id: "abb12931-1352-fd70-02c2-b78f6daab553", host: "mobvm2" },
          time_sent: "2024-06-27T06:14:48.895733Z",
          processed_at: "2024-06-27T06:14:48.912923Z",
          critical_time: "00:00:00",
          processing_time: "00:00:00",
          delivery_time: "00:00:00",
          is_system_message: false,
          conversation_id: "7de07fd4-928f-4d1a-be0f-b19c0066f22d",
          headers: [
            { key: "NServiceBus.MessageId", value: withMessageId },
            { key: "NServiceBus.MessageIntent", value: "Send" },
            { key: "NServiceBus.ConversationId", value: "7de07fd4-928f-4d1a-be0f-b19c0066f22d" },
            { key: "NServiceBus.CorrelationId", value: withMessageId },
            { key: "NServiceBus.OriginatingMachine", value: "mobvm2" },
            { key: "NServiceBus.OriginatingEndpoint", value: "Sender" },
            { key: "$.diagnostics.originating.hostid", value: "abb129311352fd7002c2b78f6daab553" },
            { key: "NServiceBus.ContentType", value: withContentType },
            { key: "NServiceBus.EnclosedMessageTypes", value: "ServiceControl.SmokeTest.SimpleCommand, ServiceControl.SmokeTest, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null" },
            { key: "NServiceBus.Version", value: "9.0.0" },
            { key: "NServiceBus.TimeSent", value: "2024-06-27 06:14:48:895733 Z" },
            { key: "NServiceBus.ProcessingMachine", value: "mobvm2" },
            { key: "NServiceBus.ProcessingEndpoint", value: "Endpoint1" },
            { key: "$.diagnostics.hostid", value: "abb129311352fd7002c2b78f6daab553" },
            { key: "$.diagnostics.hostdisplayname", value: "mobvm2" },
            { key: "NServiceBus.FailedQ", value: "Endpoint1" },
            { key: "NServiceBus.ExceptionInfo.ExceptionType", value: "System.Exception" },
            { key: "NServiceBus.ExceptionInfo.HelpLink" },
            { key: "NServiceBus.ExceptionInfo.Message", value: "SimpleHandler: Throwing exceptions enabled in Endpoint1" },
            { key: "NServiceBus.ExceptionInfo.Source", value: "ServiceControl.SmokeTest" },
            {
              key: "NServiceBus.ExceptionInfo.StackTrace",
              value:
                "System.Exception: SimpleHandler: Throwing exceptions enabled in Endpoint1\r\n   at ServiceControl.SmokeTest.SimpleHandler.Handle(SimpleCommand message, IMessageHandlerContext context) in /_/src/ServiceControl.SmokeTest/SimpleHandler.cs:line 26\r\n   at NServiceBus.InvokeHandlerTerminator.Terminate(IInvokeHandlerContext context) in /_/src/NServiceBus.Core/Pipeline/Incoming/InvokeHandlerTerminator.cs:line 33\r\n   at NServiceBus.SagaAudit.AuditInvokedSagaBehavior.Invoke(IInvokeHandlerContext context, Func`1 next) in /_/src/NServiceBus.SagaAudit/AuditInvokedSagaBehavior.cs:line 13\r\n   at NServiceBus.SagaPersistenceBehavior.Invoke(IInvokeHandlerContext context, Func`2 next) in /_/src/NServiceBus.Core/Sagas/SagaPersistenceBehavior.cs:line 41\r\n   at NServiceBus.SagaAudit.CaptureSagaStateBehavior.Invoke(IInvokeHandlerContext context, Func`1 next) in /_/src/NServiceBus.SagaAudit/CaptureSagaStateBehavior.cs:line 33\r\n   at NServiceBus.LoadHandlersConnector.Invoke(IIncomingLogicalMessageContext context, Func`2 stage) in /_/src/NServiceBus.Core/Pipeline/Incoming/LoadHandlersConnector.cs:line 44\r\n   at NServiceBus.InvokeSagaNotFoundBehavior.Invoke(IIncomingLogicalMessageContext context, Func`2 next) in /_/src/NServiceBus.Core/Sagas/InvokeSagaNotFoundBehavior.cs:line 17\r\n   at NServiceBus.DeserializeMessageConnector.Invoke(IIncomingPhysicalMessageContext context, Func`2 stage) in /_/src/NServiceBus.Core/Pipeline/Incoming/DeserializeMessageConnector.cs:line 32\r\n   at ReceivePerformanceDiagnosticsBehavior.Invoke(IIncomingPhysicalMessageContext context, Func`2 next) in /_/src/NServiceBus.Metrics/ProbeBuilders/ReceivePerformanceDiagnosticsBehavior.cs:line 18\r\n   at NServiceBus.InvokeAuditPipelineBehavior.Invoke(IIncomingPhysicalMessageContext context, Func`2 next) in /_/src/NServiceBus.Core/Audit/InvokeAuditPipelineBehavior.cs:line 19\r\n   at NServiceBus.ProcessingStatisticsBehavior.Invoke(IIncomingPhysicalMessageContext context, Func`2 next) in /_/src/NServiceBus.Core/Performance/Statistics/ProcessingStatisticsBehavior.cs:line 25\r\n   at NServiceBus.TransportReceiveToPhysicalMessageConnector.Invoke(ITransportReceiveContext context, Func`2 next) in /_/src/NServiceBus.Core/Pipeline/Incoming/TransportReceiveToPhysicalMessageConnector.cs:line 35\r\n   at NServiceBus.RetryAcknowledgementBehavior.Invoke(ITransportReceiveContext context, Func`2 next) in /_/src/NServiceBus.Core/ServicePlatform/Retries/RetryAcknowledgementBehavior.cs:line 25\r\n   at NServiceBus.MainPipelineExecutor.Invoke(MessageContext messageContext, CancellationToken cancellationToken) in /_/src/NServiceBus.Core/Pipeline/MainPipelineExecutor.cs:line 49\r\n   at NServiceBus.MainPipelineExecutor.Invoke(MessageContext messageContext, CancellationToken cancellationToken) in /_/src/NServiceBus.Core/Pipeline/MainPipelineExecutor.cs:line 68\r\n   at NServiceBus.LearningTransportMessagePump.ProcessFile(ILearningTransportTransaction transaction, String messageId, CancellationToken messageProcessingCancellationToken) in /_/src/NServiceBus.Core/Transports/Learning/LearningTransportMessagePump.cs:line 340",
            },
            { key: "NServiceBus.TimeOfFailure", value: "2024-06-27 06:14:48:912923 Z" },
            { key: "NServiceBus.ExceptionInfo.Data.Message type", value: "ServiceControl.SmokeTest.SimpleCommand" },
            { key: "NServiceBus.ExceptionInfo.Data.Handler type", value: "ServiceControl.SmokeTest.SimpleHandler" },
            { key: "NServiceBus.ExceptionInfo.Data.Handler start time", value: "2024-06-27 06:14:48:911780 Z" },
            { key: "NServiceBus.ExceptionInfo.Data.Handler failure time", value: "2024-06-27 06:14:48:911893 Z" },
            { key: "NServiceBus.ExceptionInfo.Data.Handler canceled", value: "False" },
            { key: "NServiceBus.ExceptionInfo.Data.Message ID", value: withMessageId },
            { key: "NServiceBus.ExceptionInfo.Data.Transport message ID", value: "cec43aaf-d3ac-40b9-8c3c-5c210606c016" },
            { key: "NServiceBus.ExceptionInfo.Data.Pipeline canceled", value: "False" },
          ],
          status: "failed",
          message_intent: "send",
          body_url: `/messages/${withGroupId}/body?instance_id=aHR0cDovLzEwLjAuMC41OjQ5MjAwL2FwaQ..`,
          body_size: 21,
          instance_id: "aHR0cDovLzEwLjAuMC41OjQ5MjAwL2FwaQ..",
        },
      ],
    });

    driver.mockEndpoint(`${serviceControlUrl}errors/last/${withGroupId}`, {
      body: failedMessage,
    });

    driver.mockEndpoint(`${serviceControlUrl}recoverability/groups{/:classifier}`, {
      body: [
        {
          id: withGroupId,
          title: "Endpoint1",
          type: "Endpoint Name",
          count: 1,
          first: "2024-06-27T06:14:48.912923Z",
          last: "2024-06-27T06:14:48.912923Z",
          operation_status: "None",
          operation_progress: 0,
          need_user_acknowledgement: false,
        },
      ],
    });

    //api/recoverability/groups/${withGroupId}/errors?status=unresolved&page=1&per_page=50&sort=time_of_failure&direction=desc
    driver.mockEndpoint(`${serviceControlUrl}recoverability/groups/${withGroupId}/errors`, {
      body: [failedMessage],
      headers: { "Total-Count": "1" },
    });

    driver.mockEndpoint(`${serviceControlUrl}recoverability/groups/id/${withGroupId}`, {
      body: { id: withGroupId, title: "Endpoint1", type: "Endpoint Name", count: 1, first: "2024-06-27T06:14:48.912923Z", last: "2024-06-27T06:14:48.912923Z" },
    });
  };
