import { MessageStatus } from "./Message";

export interface SagaHistory {
  id: string;
  saga_id: string;
  saga_type: string;
  changes: SagaStateChange[];
}

export interface SagaStateChange {
  start_time: Date;
  finish_time: Date;
  status: string;
  state_after_change: string;
  initiating_message: SagaMessage;
  outgoing_messages: OutgoingMessage[];
  endpoint: string;
}

export interface SagaMessage {
  message_id: string;
  is_saga_timeout_message: boolean;
  originating_endpoint: string;
  originating_machine: string;
  time_sent: Date;
  message_type: string;
  intent: string;
  body_url: string;
  message_status: MessageStatus;
}

export interface OutgoingMessage extends SagaMessage {
  delivery_delay?: string;
  destination: string;
  deliver_at: Date;
}
