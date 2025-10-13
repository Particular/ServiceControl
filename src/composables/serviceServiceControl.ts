import { reactive } from "vue";
import { useIsSupported, useIsUpgradeAvailable } from "./serviceSemVer";
import { useServiceProductUrls } from "./serviceProductUrls";
import { monitoringUrl, serviceControlUrl, useTypedFetchFromMonitoring, useTypedFetchFromServiceControl } from "./serviceServiceControlUrls";
import type RootUrls from "@/resources/RootUrls";
import type FailedMessage from "@/resources/FailedMessage";
// eslint-disable-next-line no-duplicate-imports
import { FailedMessageStatus } from "@/resources/FailedMessage";
import { useAutoRefresh } from "./useAutoRefresh";

export const stats = reactive({
  active_endpoints: 0,
  number_of_exception_groups: 0,
  number_of_failed_messages: 0,
  number_of_archived_messages: 0,
  number_of_pending_retries: 0,
  number_of_endpoints: 0,
  number_of_disconnected_endpoints: 0,
  number_of_archive_groups: 0,
});

interface ConnectionState {
  connected: boolean;
  connecting: boolean;
  connectedRecently: boolean;
  unableToConnect: boolean | null;
}
export const connectionState = reactive<ConnectionState>({
  connected: false,
  connecting: false,
  connectedRecently: false,
  unableToConnect: null,
});

export const monitoringConnectionState = reactive<ConnectionState>({
  connected: false,
  connecting: false,
  connectedRecently: false,
  unableToConnect: null,
});

export const environment = reactive({
  monitoring_version: "",
  sc_version: "",
  minimum_supported_sc_version: "6.6.0",
  is_compatible_with_sc: true,
  sp_version: window.defaultConfig && window.defaultConfig.version ? window.defaultConfig.version : "1.2.0",
  supportsArchiveGroups: false,
  endpoints_error_url: "",
  known_endpoints_url: "",
  endpoints_message_search_url: "",
  endpoints_messages_url: "",
  endpoints_url: "",
  errors_url: "",
  configuration: "",
  message_search_url: "",
  sagas_url: "",
});

export const newVersions = reactive({
  newSPVersion: {
    newspversion: false,
    newspversionlink: "",
    newspversionnumber: "",
  },
  newSCVersion: {
    newscversion: false,
    newscversionlink: "",
    newscversionnumber: "",
  },
  newMVersion: {
    newmversion: false,
    newmversionlink: "",
    newmversionnumber: "",
  },
});

interface ServiceControlInstanceConnection {
  settings: { [key: string]: object };
  errors: string[];
}

interface MetricsConnectionDetails {
  Enabled: boolean;
  MetricsQueue?: string;
  Interval?: string;
}

interface Connections {
  serviceControl: ServiceControlInstanceConnection;
  monitoring: {
    settings: MetricsConnectionDetails;
    errors: string[];
  };
}

export const connections = reactive<Connections>({
  serviceControl: {
    settings: {},
    errors: [],
  },
  monitoring: {
    settings: { Enabled: false },
    errors: [],
  },
});

export async function useServiceControl() {
  await Promise.all([useServiceControlStats(), useServiceControlMonitoringStats(), getServiceControlVersion()]);
}

export function useServiceControlAutoRefresh() {
  useAutoRefresh("serviceControlVersion", getServiceControlVersion, 60000)();
  useAutoRefresh("serviceControlStats", useServiceControlStats, 5000)();
  useAutoRefresh("serviceControlMonitoringStats", useServiceControlMonitoringStats, 5000)();
}

async function useServiceControlStats() {
  const failedMessagesResult = getFailedMessagesCount();
  const archivedMessagesResult = getArchivedMessagesCount();
  const pendingRetriesResult = getPendingRetriesCount();

  try {
    const [failedMessages, archivedMessages, pendingRetries] = await Promise.all([failedMessagesResult, archivedMessagesResult, pendingRetriesResult]);
    stats.number_of_failed_messages = failedMessages;
    stats.number_of_archived_messages = archivedMessages;
    stats.number_of_pending_retries = pendingRetries;
  } catch (err) {
    console.log(err);
  }
}

async function useServiceControlMonitoringStats() {
  const disconnectedEndpointsCountResult = getDisconnectedEndpointsCount();

  const [disconnectedEndpoints] = await Promise.all([disconnectedEndpointsCountResult]);
  //Do something here with the argument to the callback in the future if we are using them
  stats.number_of_disconnected_endpoints = disconnectedEndpoints;
}

export async function useServiceControlConnections() {
  const scConnectionResult = getServiceControlConnection();
  const monitoringConnectionResult = getMonitoringConnection();

  const [scConnection, mConnection] = await Promise.all([scConnectionResult, monitoringConnectionResult]);
  if (scConnection) {
    connections.serviceControl.settings = scConnection.settings;
    connections.serviceControl.errors = scConnection.errors;
  }
  if (mConnection) {
    connections.monitoring.settings = mConnection.Metrics;
  }
  return connections;
}

async function getServiceControlVersion() {
  const productsResult = useServiceProductUrls();
  const scResult = getPrimaryVersion();
  const mResult = setMonitoringVersion();

  const [products, scVer] = await Promise.all([productsResult, scResult, mResult]);
  if (scVer) {
    environment.supportsArchiveGroups = !!scVer.archived_groups_url;
    environment.is_compatible_with_sc = useIsSupported(environment.sc_version, environment.minimum_supported_sc_version);
    environment.endpoints_error_url = scVer && scVer.endpoints_error_url;
    environment.known_endpoints_url = scVer && scVer.known_endpoints_url;
    environment.endpoints_message_search_url = scVer.endpoints_message_search_url;
    environment.endpoints_messages_url = scVer.endpoints_messages_url;
    environment.endpoints_url = scVer.endpoints_url;
    environment.errors_url = scVer.errors_url;
    environment.configuration = scVer.configuration;
    environment.message_search_url = scVer.message_search_url;
    environment.sagas_url = scVer.sagas_url;
  }
  if (products.latestSP && useIsUpgradeAvailable(environment.sp_version, products.latestSP.tag)) {
    newVersions.newSPVersion.newspversion = true;
    newVersions.newSPVersion.newspversionlink = products.latestSP.release;
    newVersions.newSPVersion.newspversionnumber = products.latestSP.tag;
  }
  if (products.latestSC && useIsUpgradeAvailable(environment.sc_version, products.latestSC.tag)) {
    newVersions.newSCVersion.newscversion = true;
    newVersions.newSCVersion.newscversionlink = products.latestSC.release;
    newVersions.newSCVersion.newscversionnumber = products.latestSC.tag;
  }
  if (products.latestSC && useIsUpgradeAvailable(environment.monitoring_version, products.latestSC.tag)) {
    newVersions.newMVersion.newmversion = true;
    newVersions.newMVersion.newmversionlink = products.latestSC.release;
    newVersions.newMVersion.newmversionnumber = products.latestSC.tag;
  }
}

async function getServiceControlConnection() {
  try {
    const [, data] = await useTypedFetchFromServiceControl<ServiceControlInstanceConnection>("connection");
    return data;
  } catch {
    connections.serviceControl.errors = [`Error reaching ServiceControl at ${serviceControlUrl.value} connection`];
  }
}

async function getMonitoringConnection() {
  try {
    const [, data] = await useTypedFetchFromMonitoring<{ Metrics: MetricsConnectionDetails }>("connection");
    return data;
  } catch {
    connections.monitoring.errors = [`Error SC Monitoring instance at ${monitoringUrl.value}connection`];
  }
}

async function getPrimaryVersion() {
  try {
    const [response, data] = await useTypedFetchFromServiceControl<RootUrls>("");
    environment.sc_version = response.headers.get("X-Particular-Version") ?? "";
    return data;
  } catch {
    return null;
  }
}

async function setMonitoringVersion() {
  const [response] = await useTypedFetchFromMonitoring("");
  if (response) {
    environment.monitoring_version = response.headers.get("X-Particular-Version") ?? "";
  }
}

async function fetchWithErrorHandling<T, TResult>(fetchFunction: () => Promise<[Response?, T?]>, connectionState: ConnectionState, action: (response: Response, data: T) => TResult, defaultResult: TResult) {
  if (connectionState.connecting) {
    //Skip the connection state checking
    try {
      const [response, data] = await fetchFunction();
      if (response != null && data != null) {
        return await action(response, data);
      }
    } catch (err) {
      console.log(err);
      return defaultResult;
    }
  }
  try {
    if (!connectionState.connected) {
      connectionState.connecting = true;
      connectionState.connected = false;
    }

    try {
      const [response, data] = await fetchFunction();
      let result: TResult | null = null;
      if (response != null && data != null) {
        result = await action(response, data);
      }
      connectionState.unableToConnect = false;
      connectionState.connectedRecently = true;
      connectionState.connected = true;
      connectionState.connecting = false;

      if (result) {
        return result;
      }
    } catch (err) {
      connectionState.connected = false;
      connectionState.unableToConnect = true;
      connectionState.connectedRecently = false;
      connectionState.connecting = false;
      console.log(err);
    }
  } catch {
    connectionState.connecting = false;
    connectionState.connected = false;
  }

  return defaultResult;
}

function getFailedMessagesCount() {
  return getErrorMessagesCount(FailedMessageStatus.Unresolved);
}

function getPendingRetriesCount() {
  return getErrorMessagesCount(FailedMessageStatus.RetryIssued);
}

function getArchivedMessagesCount() {
  return getErrorMessagesCount(FailedMessageStatus.Archived);
}

function getErrorMessagesCount(status: FailedMessageStatus) {
  return fetchWithErrorHandling(
    () => useTypedFetchFromServiceControl<FailedMessage>(`errors?status=${status}`),
    connectionState,
    (response) => parseInt(response.headers.get("Total-Count") ?? "0"),
    0
  );
}

function getDisconnectedEndpointsCount() {
  return fetchWithErrorHandling(
    () => useTypedFetchFromMonitoring<number>("monitored-endpoints/disconnected"),
    monitoringConnectionState,

    (_, data) => {
      return data;
    },
    0
  );
}
