import { SagaHistory } from "@/resources/SagaHistory";
import { typeToName } from "@/composables/typeHumanizer";

export interface SagaMessageDataItem {
  Key: string;
  Value: string;
}

export interface SagaMessage {
  MessageFriendlyTypeName: string;
  FormattedTimeSent: string;
  Data: SagaMessageDataItem[];
  IsEventMessage: boolean;
  IsCommandMessage: boolean;
}

export interface SagaTimeoutMessage extends SagaMessage {
  TimeoutFriendly: string;
}

export interface SagaUpdateViewModel {
  StartTime: Date;
  FinishTime: Date;
  FormattedStartTime: string;
  InitiatingMessageType: string;
  FormattedInitiatingMessageTimestamp: string;
  Status: string;
  StatusDisplay: string;
  HasTimeout: boolean;
  IsFirstNode: boolean;
  NonTimeoutMessages: SagaMessage[];
  TimeoutMessages: SagaTimeoutMessage[];
  HasNonTimeoutMessages: boolean;
  HasTimeoutMessages: boolean;
}

export interface SagaViewModel {
  SagaTitle: string;
  SagaGuid: string;
  MessageIdUrl: string;
  ParticipatedInSaga: boolean;
  HasSagaData: boolean;
  ShowNoPluginActiveLegend: boolean;
  SagaCompleted: boolean;
  FormattedCompletionTime: string;
  SagaUpdates: SagaUpdateViewModel[];
  ShowMessageData: boolean;
}

export function parseSagaUpdates(sagaHistory: SagaHistory | null): SagaUpdateViewModel[] {
  if (!sagaHistory || !sagaHistory.changes || !sagaHistory.changes.length) return [];

  return sagaHistory.changes
    .map((update) => {
      const startTime = new Date(update.start_time);
      const finishTime = new Date(update.finish_time);
      const initiatingMessageTimestamp = new Date(update.initiating_message?.time_sent || Date.now());

      // Create common base message objects with shared properties
      const outgoingMessages = update.outgoing_messages.map((msg) => {
        const delivery_delay = msg.delivery_delay || "00:00:00";
        const timeSent = new Date(msg.time_sent);
        const hasTimeout = !!delivery_delay && delivery_delay !== "00:00:00";
        const timeoutSeconds = delivery_delay.split(":")[2] || "0";
        const isEventMessage = msg.intent === "Publish";

        return {
          MessageType: msg.message_type || "",
          MessageId: msg.message_id,
          FormattedTimeSent: `${timeSent.toLocaleDateString()} ${timeSent.toLocaleTimeString()}`,
          HasTimeout: hasTimeout,
          TimeoutSeconds: timeoutSeconds,
          MessageFriendlyTypeName: typeToName(msg.message_type || ""),
          Data: [] as SagaMessageDataItem[],
          IsEventMessage: isEventMessage,
          IsCommandMessage: !isEventMessage,
        };
      });

      const timeoutMessages = outgoingMessages
        .filter((msg) => msg.HasTimeout)
        .map(
          (msg) =>
            ({
              ...msg,
              TimeoutFriendly: `${msg.TimeoutSeconds}s`, //TODO: Update with logic from ServiceInsight
            }) as SagaTimeoutMessage
        );

      const nonTimeoutMessages = outgoingMessages.filter((msg) => !msg.HasTimeout) as SagaMessage[];

      const hasTimeout = timeoutMessages.length > 0;

      return {
        StartTime: startTime,
        FinishTime: finishTime,
        FormattedStartTime: `${startTime.toLocaleDateString()} ${startTime.toLocaleTimeString()}`,
        Status: update.status,
        StatusDisplay: update.status === "new" ? "Saga Initiated" : "Saga Updated",
        InitiatingMessageType: typeToName(update.initiating_message?.message_type || "Unknown Message") || "",
        FormattedInitiatingMessageTimestamp: `${initiatingMessageTimestamp.toLocaleDateString()} ${initiatingMessageTimestamp.toLocaleTimeString()}`,
        HasTimeout: hasTimeout,
        IsFirstNode: update.status === "new",
        TimeoutMessages: timeoutMessages,
        NonTimeoutMessages: nonTimeoutMessages,
        HasNonTimeoutMessages: nonTimeoutMessages.length > 0,
        HasTimeoutMessages: timeoutMessages.length > 0,
      };
    })
    .sort((a, b) => a.StartTime.getTime() - b.StartTime.getTime())
    .sort((a, b) => a.FinishTime.getTime() - b.FinishTime.getTime());
}
