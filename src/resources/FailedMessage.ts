import type EndpointDetails from "@/resources/EndpointDetails";
import type Header from "@/resources/Header";

export default interface FailedMessage {
  id: string;
  message_type: string;
  time_sent?: string;
  is_system_message: boolean;
  exception: ExceptionDetails;
  message_id: string;
  number_of_processing_attempts: number;
  status: FailedMessageStatus;
  sending_endpoint: EndpointDetails;
  receiving_endpoint: EndpointDetails;
  queue_address: string;
  time_of_failure: string;
  last_modified: string;
  edited: boolean;
  edit_of: string;
}

export interface ExtendedFailedMessage extends FailedMessage {
  error_retention_period: number;
  delete_soon: boolean;
  deleted_in: string;
  retryInProgress: boolean;
  deleteInProgress: boolean;
  restoreInProgress: boolean;
  selected: boolean;
  retried: boolean;
  archiving: boolean;
  restoring: boolean;
  archived: boolean;
  resolved: boolean;
  headersNotFound: boolean;
  messageBodyNotFound: boolean;
  bodyUnavailable: boolean;
  headers: Header[];
  conversationId: string;
  messageBody: string;
  isEditAndRetryEnabled: boolean;
  redirect: boolean;
  submittedForRetrial: boolean;
}

export interface FailedMessageError {
  notFound: boolean;
  error: boolean;
}

export function isError(obj: ExtendedFailedMessage | FailedMessageError): obj is FailedMessageError {
  return (obj as FailedMessageError).error !== undefined || (obj as FailedMessageError).notFound !== undefined;
}

export interface ExceptionDetails {
  exception_type: string;
  message: string;
  source: string;
  stack_trace: string;
}

export enum FailedMessageStatus {
  Unresolved = "unresolved",
  Resolved = "resolved",
  RetryIssued = "retryIssued",
  Archived = "archived",
}
