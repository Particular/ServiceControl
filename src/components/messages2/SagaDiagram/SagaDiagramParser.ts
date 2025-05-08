import { SagaHistory } from "@/resources/SagaHistory";
import { typeToName } from "@/composables/typeHumanizer";
import { SagaMessageData } from "@/stores/SagaDiagramStore";
import { getTimeoutFriendly } from "@/composables/deliveryDelayParser";

export interface SagaMessageViewModel {
  MessageId: string;
  FriendlyTypeName: string;
  FormattedTimeSent: string;
  Data: SagaMessageData;
  IsEventMessage: boolean;
  IsCommandMessage: boolean;
}
export interface InitiatingMessageViewModel {
  FriendlyTypeName: string;
  IsSagaTimeoutMessage: boolean;
  FormattedMessageTimestamp: string;
  IsEventMessage: boolean;
  MessageData: SagaMessageData;
  HasRelatedTimeoutRequest?: boolean;
  MessageId: string;
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

  const timeoutMessageIds = new Set<string>();
  sagaHistory.changes.forEach((update) => {
    if (update.outgoing_messages) {
      update.outgoing_messages.forEach((msg) => {
        const delivery_delay = msg.delivery_delay || "00:00:00";
        if (delivery_delay && delivery_delay !== "00:00:00") {
          timeoutMessageIds.add(msg.message_id);
        }
      });
    }
  });

  const updates = sagaHistory.changes
    .map((update) => {
      const startTime = new Date(update.start_time);
      const finishTime = new Date(update.finish_time);
      const initiatingMessageTimestamp = new Date(update.initiating_message?.time_sent || Date.now());

      // Find message data for initiating message
      const initiatingMessageData = update.initiating_message ? findMessageData(messagesData, update.initiating_message.message_id) : createEmptyMessageData();

      // Create common base message objects with shared properties
      const outgoingMessages = update.outgoing_messages.map((msg) => {
        const delivery_delay = msg.delivery_delay || "00:00:00";
        const timeSent = new Date(msg.time_sent);
        const hasTimeout = !!delivery_delay && delivery_delay !== "00:00:00";
        const timeoutSeconds = delivery_delay.split(":")[2] || "0";
        const isEventMessage = msg.intent === "Publish";

        // Find corresponding message data
        const messageData = findMessageData(messagesData, msg.message_id);
        return {
          MessageType: msg.message_type || "",
          MessageId: msg.message_id,
          FormattedTimeSent: `${timeSent.toLocaleDateString()} ${timeSent.toLocaleTimeString()}`,
          HasTimeout: hasTimeout,
          TimeoutSeconds: timeoutSeconds,
          TimeoutFriendly: getTimeoutFriendly(delivery_delay),
          FriendlyTypeName: typeToName(msg.message_type || ""),
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

      // Check if initiating message is a timeout and if so, if it has a corresponding request in the diagram
      const hasRelatedTimeoutRequest = update.initiating_message?.is_saga_timeout_message && timeoutMessageIds.has(update.initiating_message?.message_id);

      return <SagaUpdateViewModel>{
        MessageId: update.initiating_message?.message_id || "",
        StartTime: startTime,
        FinishTime: finishTime,
        FormattedStartTime: `${startTime.toLocaleDateString()} ${startTime.toLocaleTimeString()}`,
        Status: update.status,
        StatusDisplay: update.status === "new" ? "Saga Initiated" : "Saga Updated",
        InitiatingMessage: <InitiatingMessageViewModel>{
          FriendlyTypeName: typeToName(update.initiating_message?.message_type || "Unknown Message") || "",
          MessageId: update.initiating_message?.message_id || "",
          FormattedMessageTimestamp: `${initiatingMessageTimestamp.toLocaleDateString()} ${initiatingMessageTimestamp.toLocaleTimeString()}`,
          MessageData: initiatingMessageData,
          IsEventMessage: update.initiating_message?.intent === "Publish",
          IsSagaTimeoutMessage: update.initiating_message?.is_saga_timeout_message || false,
          HasRelatedTimeoutRequest: hasRelatedTimeoutRequest,
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

// Helper function to find message data or create empty data if not found
function findMessageData(messagesData: SagaMessageData[], messageId: string): SagaMessageData {
  const messageData = messagesData.find((m) => m.message_id === messageId);
  return messageData || createEmptyMessageData();
}

// Helper function to create an empty message data object
function createEmptyMessageData(): SagaMessageData {
  return {
    message_id: "",
    body: {
      data: {},
      loading: false,
      failed_to_load: false,
      not_found: false,
    },
  };
}
