import HistoricRetryOperation from "./HistoricRetryOperation";
import UnacknowledgedRetryOperation from "./UnacknowledgedRetryOperation";

export default interface RecoverabilityHistoryResponse {
  id: string;
  historic_operations: HistoricRetryOperation[];
  unacknowledged_operations: UnacknowledgedRetryOperation[];
}
