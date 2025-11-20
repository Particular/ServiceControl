import type Header from "./Header";

export interface HeaderWithEditing extends Header {
  isLocked: boolean;
  isSensitive: boolean;
  isMarkedAsRemoved: boolean;
  isChanged: boolean;
}

export interface EditedMessage {
  messageBody: string;
  headers: HeaderWithEditing[];
}
