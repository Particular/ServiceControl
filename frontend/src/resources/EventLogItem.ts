export default interface EventLogItem {
  id: string;
  description: string;
  severity: Severity;
  raised_at: string;
  related_to: string[];
  category: string;
  event_type: string;
}

export enum Severity {
  Critical = "critical",
  Error = "error",
  Warning = "warning",
  Info = "info",
}
