import EndpointDetails from "../EndpointDetails";
import { NServiceBusHeaders } from "../Header";
import Message, { MessageIntent, MessageStatus } from "../Message";
import { Handler } from "./Handler";
import { friendlyTypeName } from "./SequenceModel";

export interface RoutedMessage {
  id: string;
  name: string;
  readonly selectedMessage: Message;
  fromHandler?: Handler;
  toHandler?: Handler;
  route?: MessageProcessingRoute;
  direction: Direction;
  type: RoutedMessageType;
  readonly receiving: EndpointDetails;
  readonly sending: EndpointDetails;
  readonly sentTime: Date | undefined;
  readonly messageId: string;
  readonly status: MessageStatus;
}

export interface MessageProcessingRoute {
  readonly name?: string;
  readonly fromRoutedMessage?: RoutedMessage;
  readonly processingHandler?: Handler;
}

export enum Direction {
  Left,
  Right,
}

export enum RoutedMessageType {
  Event,
  Command,
  Local,
  Timeout,
}

export function createRoute(routedMessage: RoutedMessage, processingHandler: Handler): MessageProcessingRoute {
  return new MessageProcessingRouteItem(routedMessage, processingHandler);
}

export function createRoutedMessage(message: Message): RoutedMessage {
  const routedMessage = new RoutedMessageItem(message);

  if (message.message_intent === MessageIntent.Publish) routedMessage.type = RoutedMessageType.Event;
  else {
    const isTimeoutString = message.headers.find((h) => h.key === NServiceBusHeaders.IsSagaTimeoutMessage)?.value;
    const isTimeout = (isTimeoutString ?? "") === "true";
    if (isTimeout) routedMessage.type = RoutedMessageType.Timeout;
    else if (message.receiving_endpoint.host_id === message.sending_endpoint.host_id && message.receiving_endpoint.name === message.sending_endpoint.name) routedMessage.type = RoutedMessageType.Local;
    else routedMessage.type = RoutedMessageType.Command;
  }

  return routedMessage;
}

class MessageProcessingRouteItem implements MessageProcessingRoute {
  readonly name?: string;
  private _fromRoutedMessage?: RoutedMessageItem;
  readonly processingHandler?: Handler;

  constructor(routedMessage?: RoutedMessageItem, processingHandler?: Handler) {
    this._fromRoutedMessage = routedMessage;
    this.processingHandler = processingHandler;

    if (routedMessage && this.processingHandler) {
      this.name = `${processingHandler?.name}(${routedMessage.id})`;
    }

    if (routedMessage) routedMessage.route = this;
    if (processingHandler) processingHandler.route = this;
  }

  get fromRoutedMessage() {
    return this._fromRoutedMessage as RoutedMessage | undefined;
  }
}

class RoutedMessageItem implements RoutedMessage {
  readonly selectedMessage: Message;
  readonly name: string;
  fromHandler?: Handler;
  toHandler?: Handler;
  route?: MessageProcessingRoute;
  direction = Direction.Left;
  type = RoutedMessageType.Command;

  constructor(message: Message) {
    this.selectedMessage = message;
    this.name = friendlyTypeName(message.message_type) ?? "";
  }

  get id() {
    return this.selectedMessage.id;
  }

  get receiving() {
    return this.selectedMessage.receiving_endpoint;
  }
  get sending() {
    return this.selectedMessage.sending_endpoint;
  }
  get sentTime() {
    return this.selectedMessage.time_sent ? new Date(this.selectedMessage.time_sent) : undefined;
  }
  get messageId() {
    return this.selectedMessage.message_id;
  }
  get status() {
    return this.selectedMessage.status;
  }
}
