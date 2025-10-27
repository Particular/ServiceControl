import { acceptHMRUpdate, defineStore, storeToRefs } from "pinia";
import { computed, onMounted, onUnmounted, reactive, ref } from "vue";
import { FailedMessage, FailedMessageStatus } from "@/resources/FailedMessage";
import { ConnectionState } from "@/resources/ConnectionState";
import { useCounter } from "@vueuse/core";
import { useServiceControlStore } from "./ServiceControlStore";

export const useConnectionsAndStatsStore = defineStore("ConnectionsAndStatsStore", () => {
  const serviceControlStore = useServiceControlStore();
  const { isMonitoringEnabled } = storeToRefs(serviceControlStore);

  const failedMessageCount = ref(0);
  const archivedMessageCount = ref(0);
  const pendingRetriesMessageCount = ref(0);
  const disconnectedEndpointsCount = ref(0);

  const { count: requiresFullFailureDetailsSubscriberCount, inc, dec } = useCounter(0);
  function requiresFullFailureDetails() {
    onMounted(() => inc());
    onUnmounted(() => dec());
  }

  const connectionState = reactive<ConnectionState>({
    connected: false,
    connecting: false,
    connectedRecently: false,
    unableToConnect: null,
  });

  const monitoringConnectionState = reactive<ConnectionState>({
    connected: false,
    connecting: false,
    connectedRecently: false,
    unableToConnect: null,
  });

  const displayConnectionsWarning = computed(() => (connectionState.unableToConnect || (monitoringConnectionState.unableToConnect && isMonitoringEnabled.value)) ?? false);

  async function refresh() {
    const failedMessagesResult = getErrorMessagesCount(FailedMessageStatus.Unresolved);
    const archivedMessagesResult = requiresFullFailureDetailsSubscriberCount.value > 0 ? getErrorMessagesCount(FailedMessageStatus.Archived) : 0;
    const pendingRetriesResult = requiresFullFailureDetailsSubscriberCount.value > 0 ? getErrorMessagesCount(FailedMessageStatus.RetryIssued) : 0;
    const disconnectedEndpointsCountResult = getDisconnectedEndpointsCount();

    const [failedMessages, archivedMessages, pendingRetries, disconnectedEndpoints] = await Promise.all([failedMessagesResult, archivedMessagesResult, pendingRetriesResult, disconnectedEndpointsCountResult]);

    failedMessageCount.value = failedMessages;
    archivedMessageCount.value = archivedMessages;
    pendingRetriesMessageCount.value = pendingRetries;
    disconnectedEndpointsCount.value = disconnectedEndpoints;
  }

  function getErrorMessagesCount(status: FailedMessageStatus) {
    return fetchAndSetConnectionState(
      () => serviceControlStore.fetchTypedFromServiceControl<FailedMessage>(`errors?status=${status}`),
      connectionState,
      (response) => parseInt(response.headers.get("Total-Count") ?? "0"),
      0
    );
  }

  function getDisconnectedEndpointsCount() {
    return fetchAndSetConnectionState(
      () => serviceControlStore.fetchTypedFromMonitoring<number>("monitored-endpoints/disconnected"),
      monitoringConnectionState,
      (_, data) => {
        return data;
      },
      0
    );
  }

  return {
    refresh,
    failedMessageCount,
    requiresFullFailureDetails,
    archivedMessageCount,
    pendingRetriesMessageCount,
    disconnectedEndpointsCount,
    connectionState,
    monitoringConnectionState,
    displayConnectionsWarning,
  };
});

async function fetchAndSetConnectionState<T, TResult>(fetchFunction: () => Promise<[Response?, T?]>, connectionState: ConnectionState, action: (response: Response, data: T) => TResult, defaultResult: TResult) {
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

if (import.meta.hot) {
  import.meta.hot.accept(acceptHMRUpdate(useConnectionsAndStatsStore, import.meta.hot));
}

export type ConnectionsAndStatsStore = ReturnType<typeof useConnectionsAndStatsStore>;
