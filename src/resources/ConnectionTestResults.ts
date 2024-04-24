export default interface ConnectionTestResults {
  transport: string;
  audit_connection_result: ConnectionSettingsTestResult;
  monitoring_connection_result: ConnectionSettingsTestResult;
  broker_connection_result: ConnectionSettingsTestResult;
}

export interface ConnectionSettingsTestResult {
  connection_successful: boolean;
  connection_error_messages: string[];
  diagnostics: string;
}
