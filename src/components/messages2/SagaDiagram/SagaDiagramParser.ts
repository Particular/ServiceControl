import { SagaHistory } from "@/resources/SagaHistory";
import { typeToName } from "@/composables/typeHumanizer";
import { SagaMessageData, SagaMessageDataItem } from "@/stores/SagaDiagramStore";
import { getTimeoutFriendly } from "@/composables/deliveryDelayParser";

export interface SagaMessageViewModel {
  MessageId: string;
  MessageFriendlyTypeName: string;
  FormattedTimeSent: string;
  Data: SagaMessageDataItem[];
  IsEventMessage: boolean;
  IsCommandMessage: boolean;
}
export interface InitiatingMessageViewModel {
  MessageType: string;
  IsSagaTimeoutMessage: boolean;
  FormattedMessageTimestamp: string;
  MessageData: SagaMessageDataItem[];
}
export interface SagaTimeoutMessageViewModel extends SagaMessageViewModel {
  TimeoutFriendly: string;
  HasBeenProcessed: boolean;
}

export interface SagaUpdateViewModel {
  MessageId: string;
  StartTime: Date;
  FinishTime: Date;
  FormattedStartTime: string;
  InitiatingMessage: InitiatingMessageViewModel;
  Status: string;
  StatusDisplay: string;
  HasTimeout: boolean;
  IsFirstNode: boolean;
  OutgoingMessages: SagaMessageViewModel[];
  OutgoingTimeoutMessages: SagaTimeoutMessageViewModel[];
  HasOutgoingMessages: boolean;
  HasOutgoingTimeoutMessages: boolean;
  showUpdatedPropertiesOnly: boolean;
  stateAfterChange: string;
  previousStateAfterChange?: string;
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

export function parseSagaUpdates(sagaHistory: SagaHistory | null, messagesData: SagaMessageData[]): SagaUpdateViewModel[] {
  if (!sagaHistory || !sagaHistory.changes || !sagaHistory.changes.length) return [];

  const updates = sagaHistory.changes
    .map((update) => {
      const startTime = new Date(update.start_time);
      const finishTime = new Date(update.finish_time);
      const initiatingMessageTimestamp = new Date(update.initiating_message?.time_sent || Date.now());

      // Find message data for initiating message
      const initiatingMessageData = update.initiating_message ? messagesData.find((m) => m.message_id === update.initiating_message.message_id)?.data || [] : [];

      // Create common base message objects with shared properties
      const outgoingMessages = update.outgoing_messages.map((msg) => {
        const delivery_delay = msg.delivery_delay || "00:00:00";
        const timeSent = new Date(msg.time_sent);
        const hasTimeout = !!delivery_delay && delivery_delay !== "00:00:00";
        const timeoutSeconds = delivery_delay.split(":")[2] || "0";
        const isEventMessage = msg.intent === "Publish";

        // Find corresponding message data
        const messageData = messagesData.find((m) => m.message_id === msg.message_id)?.data || [];
        return {
          MessageType: msg.message_type || "",
          MessageId: msg.message_id,
          FormattedTimeSent: `${timeSent.toLocaleDateString()} ${timeSent.toLocaleTimeString()}`,
          HasTimeout: hasTimeout,
          TimeoutSeconds: timeoutSeconds,
          TimeoutFriendly: getTimeoutFriendly(delivery_delay),
          MessageFriendlyTypeName: typeToName(msg.message_type || ""),
          Data: messageData,
          IsEventMessage: isEventMessage,
          IsCommandMessage: !isEventMessage,
        };
      });

      const outgoingTimeoutMessages = outgoingMessages
        .filter((msg) => msg.HasTimeout)
        .map((msg) => {
          // Check if this timeout message has been processed by checking if there's an initiating message with matching ID
          const hasBeenProcessed = sagaHistory.changes.some((update) => update.initiating_message?.message_id === msg.MessageId);

          return {
            ...msg,
            TimeoutFriendly: `${msg.TimeoutFriendly}`,
            HasBeenProcessed: hasBeenProcessed,
          } as SagaTimeoutMessageViewModel;
        });

      const regularMessages = outgoingMessages.filter((msg) => !msg.HasTimeout) as SagaMessageViewModel[];

      const hasTimeout = outgoingTimeoutMessages.length > 0;

      return <SagaUpdateViewModel>{
        MessageId: update.initiating_message?.message_id || "",
        StartTime: startTime,
        FinishTime: finishTime,
        FormattedStartTime: `${startTime.toLocaleDateString()} ${startTime.toLocaleTimeString()}`,
        Status: update.status,
        StatusDisplay: update.status === "new" ? "Saga Initiated" : "Saga Updated",
        InitiatingMessage: <InitiatingMessageViewModel>{
          MessageType: typeToName(update.initiating_message?.message_type || "Unknown Message") || "",
          FormattedMessageTimestamp: `${initiatingMessageTimestamp.toLocaleDateString()} ${initiatingMessageTimestamp.toLocaleTimeString()}`,
          MessageData: initiatingMessageData,
          IsSagaTimeoutMessage: update.initiating_message?.is_saga_timeout_message || false,
        },
        HasTimeout: hasTimeout,
        IsFirstNode: update.status === "new",
        OutgoingTimeoutMessages: outgoingTimeoutMessages,
        OutgoingMessages: regularMessages,
        HasOutgoingMessages: regularMessages.length > 0,
        HasOutgoingTimeoutMessages: outgoingTimeoutMessages.length > 0,
        showUpdatedPropertiesOnly: true, // Default to showing only updated properties
        stateAfterChange: update.state_after_change || "{}",
      };
    })
    .sort((a, b) => a.StartTime.getTime() - b.StartTime.getTime())
    .sort((a, b) => a.FinishTime.getTime() - b.FinishTime.getTime());

  // Add reference to previous state for each update except the first one
  for (let i = 1; i < updates.length; i++) {
    updates[i].previousStateAfterChange = updates[i - 1].stateAfterChange;
  }

  return updates;
}
