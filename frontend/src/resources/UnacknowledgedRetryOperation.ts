import { RetryType } from "./RetryType";

export default interface UnacknowledgedRetryOperation {
  request_id: string;
  retry_type: RetryType;
  start_time: string;
  completion_time: string;
  last: string;
  originator: string;
  classifier: string;
  failed: boolean;
  number_of_messages_processed: number;
}
