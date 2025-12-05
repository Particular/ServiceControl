export interface ConnectionState {
  connected: boolean;
  connecting: boolean;
  connectedRecently: boolean;
  unableToConnect: boolean | null;
}
