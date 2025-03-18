import { useTypedFetchFromServiceControl } from "@/composables/serviceServiceControlUrls";
import { acceptHMRUpdate, defineStore } from "pinia";
import { ref, watch } from "vue";
import useAutoRefresh from "@/composables/autoRefresh";
import type { SortInfo } from "@/components/SortInfo";
import Message from "@/resources/Message";

export enum ColumnNames {
  Status = "status",
  MessageId = "messageId",
  MessageType = "messageType",
  TimeSent = "timeSent",
  ProcessingTime = "processingTime",
}

const columnSortings = new Map<string, string>([
  [ColumnNames.Status, "status"],
  [ColumnNames.MessageId, "id"],
  [ColumnNames.MessageType, "message_type"],
  [ColumnNames.TimeSent, "time_sent"],
  [ColumnNames.ProcessingTime, "processing_time"],
]);

export const useAuditStore = defineStore("AuditStore", () => {
  const sortByInstances = ref<SortInfo>({
    property: ColumnNames.TimeSent,
    isAscending: false,
  });

  const messageFilterString = ref("");
  const itemsPerPage = ref(35);
  const selectedPage = ref(1);
  const totalCount = ref(0);
  const messages = ref<Message[]>([]);
  // const filteredMessages = computed<Message[]>(() => sortedMessages.value.filter((message) => !messageFilterString.value || message.id.toLowerCase().includes(messageFilterString.value.toLowerCase())));
  watch(messageFilterString, (newValue) => {
    setMessageFilterString(newValue);
  });

  const dataRetriever = useAutoRefresh(async () => {
    try {
      const [response, data] = await useTypedFetchFromServiceControl<Message[]>(
        `messages/?include_system_messages=false&per_page=${itemsPerPage.value}&page=${selectedPage.value}&sort=${columnSortings.get(sortByInstances.value.property)}&direction=${sortByInstances.value.isAscending ? "asc" : "desc"}`
      );
      totalCount.value = parseInt(response.headers.get("total-count") ?? "0");
      messages.value = data;
    } catch (e) {
      messages.value = [];
      throw e;
    }
  }, null);

  const refresh = dataRetriever.executeAndResetTimer;
  watch([itemsPerPage, selectedPage, sortByInstances], () => refresh());

  function setMessageFilterString(filter: string) {
    messageFilterString.value = filter;
  }

  return {
    refresh,
    updateRefreshTimer: dataRetriever.updateTimeout,
    sortByInstances,
    messages,
    messageFilterString,
    itemsPerPage,
    selectedPage,
    totalCount,
  };
});

if (import.meta.hot) {
  import.meta.hot.accept(acceptHMRUpdate(useAuditStore, import.meta.hot));
}

export type AuditStore = ReturnType<typeof useAuditStore>;
