export default interface GroupOperation {
  id: string;
  title: string;
  type: string;
  count: number;
  operation_messages_completed_count?: number;
  comment: string;
  first?: string;
  last?: string;
  operation_status: string;
  operation_failed?: boolean;
  operation_progress: number;
  operation_remaining_count?: number;
  operation_startTime?: string;
  operation_completion_time?: string;
  need_user_acknowledgement: boolean;
  last_operation_completion_time?: string;
}
