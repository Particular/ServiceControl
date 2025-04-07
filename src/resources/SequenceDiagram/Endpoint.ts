import { NServiceBusHeaders } from "../Header";
import Message from "../Message";
import { Handler } from "./Handler";

export interface Endpoint {
  readonly name: string;
  readonly hosts: EndpointHost[];
  readonly hostId: string;
  readonly handlers: Handler[];
  addHandler(handler: Handler): void;
}

export interface EndpointHost {
  readonly host: string;
  readonly hostId: string;
  readonly versions: string[];
}

export function createProcessingEndpoint(message: Message): Endpoint {
  return new EndpointItem(
    message.receiving_endpoint.name,
    message.receiving_endpoint.host,
    message.receiving_endpoint.host_id,
    message.receiving_endpoint.name === message.sending_endpoint.name && message.receiving_endpoint.host === message.sending_endpoint.host ? message.headers.find((h) => h.key === NServiceBusHeaders.NServiceBusVersion)?.value : undefined
  );
}

export function createSendingEndpoint(message: Message): Endpoint {
  return new EndpointItem(message.sending_endpoint.name, message.sending_endpoint.host, message.sending_endpoint.host_id, message.headers.find((h) => h.key === NServiceBusHeaders.NServiceBusVersion)?.value);
}

export class EndpointRegistry {
  #store = new Map<string, EndpointItem>();

  register(item: Endpoint) {
    let endpoint = this.#store.get(item.name);
    if (!endpoint) {
      endpoint = item as EndpointItem;
      this.#store.set(endpoint.name, endpoint);
    }

    item.hosts.forEach((host) => endpoint.addHost(host as Host));
  }

  get(item: Endpoint) {
    return this.#store.get(item.name)! as Endpoint;
  }
}

class EndpointItem implements Endpoint {
  private _hosts: Map<string, Host>;
  private _name: string;
  private _handlers: Handler[] = [];

  constructor(name: string, host: string, id: string, version?: string) {
    const initialHost = new Host(host, id, version);
    this._hosts = new Map<string, Host>([[initialHost.equatableKey, initialHost]]);
    this._name = name;
  }

  get name() {
    return this._name;
  }
  get hosts() {
    return [...this._hosts].map(([, host]) => host);
  }
  get host() {
    return [...this._hosts].map(([, host]) => host.host).join(",");
  }
  get hostId() {
    return [...this._hosts].map(([, host]) => host.hostId).join(",");
  }
  get handlers() {
    return [...this._handlers];
  }

  addHost(host: Host) {
    if (!this._hosts.has(host.equatableKey)) {
      this._hosts.set(host.equatableKey, host);
    } else {
      const existing = this._hosts.get(host.equatableKey)!;
      existing.addVersions(host.versions);
    }
  }

  addHandler(handler: Handler) {
    this._handlers.push(handler);
  }
}

class Host implements EndpointHost {
  private _host: string;
  private _hostId: string;
  private _versions: Set<string>;

  constructor(host: string, hostId: string, version?: string) {
    this._host = host;
    this._hostId = hostId;
    this._versions = new Set<string>();
    this.addVersions([version]);
  }

  get host() {
    return this._host;
  }
  get hostId() {
    return this._hostId;
  }

  get versions() {
    return [...this._versions];
  }

  get equatableKey() {
    return `${this._hostId}###${this._host}`;
  }

  addVersions(versions: (string | undefined)[]) {
    versions.filter((version) => version).forEach((version) => this._versions.add(version!.toLowerCase()));
  }
}
