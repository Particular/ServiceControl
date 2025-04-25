import { acceptHMRUpdate, defineStore } from "pinia";
import { ref, watch } from "vue";
import { SagaHistory, SagaMessage } from "@/resources/SagaHistory";
import { useFetchFromServiceControl } from "@/composables/serviceServiceControlUrls";
import Message from "@/resources/Message";

const StandardKeys = ["$type", "Id", "Originator", "OriginalMessageId"];
export interface SagaMessageDataItem {
  key: string;
  value: string;
}
export interface SagaMessageData {
  message_id: string;
  data: SagaMessageDataItem[];
}
export const useSagaDiagramStore = defineStore("SagaDiagramStore", () => {
  const sagaHistory = ref<SagaHistory | null>(null);
  const sagaId = ref<string | null>(null);
  const loading = ref(false);
  const messageDataLoading = ref(false);
  const error = ref<string | null>(null);
  const showMessageData = ref(false);
  const fetchedMessages = ref(new Set<string>());
  const messagesData = ref<SagaMessageData[]>([]);
  const MessageBodyEndpoint = "messages/{0}/body";

  // Watch the sagaId and fetch saga history when it changes
  watch(sagaId, async (newSagaId) => {
    if (newSagaId) {
      await fetchSagaHistory(newSagaId);
    } else {
      clearSagaHistory();
    }
  });

  // Watch both showMessageData and sagaHistory together
  watch([showMessageData, sagaHistory], async ([show, history]) => {
    if (show && history) {
      await fetchMessagesData(history);
    }
  });

  function setSagaId(id: string | null) {
    sagaId.value = id;
  }

  async function fetchSagaHistory(id: string) {
    if (!id) return;

    loading.value = true;
    error.value = null;

    try {
      const response = await useFetchFromServiceControl(`sagas/${id}`);

      if (response.status === 404) {
        sagaHistory.value = null;
        error.value = "Saga history not found";
      } else if (!response.ok) {
        sagaHistory.value = null;
        error.value = "Failed to fetch saga history";
      } else {
        const data = await response.json();
        sagaHistory.value = data;
      }
    } catch (e) {
      error.value = e instanceof Error ? e.message : "Unknown error occurred";
      sagaHistory.value = null;
    } finally {
      loading.value = false;
    }
  }

  function createEmptyMessageData(message_id: string): SagaMessageData {
    return {
      message_id,
      data: [],
    };
  }

  async function fetchSagaMessageData(message: SagaMessage): Promise<SagaMessageData> {
    const bodyUrl = (message.body_url ?? formatUrl(MessageBodyEndpoint, message.message_id)).replace(/^\//, "");

    try {
      const response = await useFetchFromServiceControl(bodyUrl, { cache: "no-store" });
      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }
      const body = await response.json();

      if (!body) {
        return createEmptyMessageData(message.message_id);
      }

      let data: SagaMessageDataItem[];
      if (typeof body === "string" && body.trim().startsWith("<?xml")) {
        data = getXmlData(body);
      } else {
        data = processJsonValues(body);
      }
      // Check if parsed data is empty
      if (!data || data.length === 0) {
        return createEmptyMessageData(message.message_id);
      }
      return {
        message_id: message.message_id,
        data,
      };
    } catch (e) {
      error.value = e instanceof Error ? e.message : "Unknown error occurred";
      return createEmptyMessageData(message.message_id);
    }
  }

  async function getAuditMessages(sagaId: string) {
    try {
      const response = await useFetchFromServiceControl(`messages/search?q=${sagaId}`);
      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }
      return await response.json();
    } catch (error) {
      console.error("Error fetching audit messages:", error);
      return { result: [] };
    }
  }

  function getXmlData(xmlString: string): SagaMessageDataItem[] {
    try {
      const parser = new DOMParser();
      const xmlDoc = parser.parseFromString(xmlString, "application/xml");

      // Get the root element
      const rootElement = xmlDoc.documentElement;
      if (!rootElement) {
        return [];
      }

      // Handle both v5 and pre-v5 message formats
      const messageRoot = rootElement.nodeName === "Messages" ? Array.from(rootElement.children)[0] : rootElement;

      if (!messageRoot) {
        return [];
      }

      // Convert child elements to SagaMessageDataItems
      return Array.from(messageRoot.children).map((node) => ({
        key: node.nodeName,
        value: node.textContent?.trim() || "",
      }));
    } catch (error) {
      console.error("Error parsing message data:", error);
      return [];
    }
  }

  // Replace or modify the existing processJsonValues function
  function processJsonValues(jsonBody: string | Record<string, unknown>): SagaMessageDataItem[] {
    let parsedBody: Record<string, unknown>;
    if (typeof jsonBody === "string") {
      try {
        parsedBody = JSON.parse(jsonBody);
      } catch (e) {
        console.error("Error parsing JSON:", e);
        return [];
      }
    } else {
      parsedBody = jsonBody;
    }

    const items: SagaMessageDataItem[] = [];

    for (const key in parsedBody) {
      if (!StandardKeys.includes(key)) {
        items.push({
          key: key,
          value: String(parsedBody[key] ?? ""),
        });
      }
    }

    return items;
  }

  function clearSagaHistory() {
    sagaHistory.value = null;
    sagaId.value = null;
    error.value = null;
    fetchedMessages.value.clear();
    messagesData.value = [];
  }

  function formatUrl(template: string, id: string): string {
    return template.replace("{0}", id);
  }

  function toggleMessageData() {
    showMessageData.value = !showMessageData.value;
  }

  async function fetchMessagesData(history: SagaHistory) {
    messageDataLoading.value = true;
    error.value = null;

    try {
      // Get all messages from changes array - both initiating and outgoing
      const messagesToFetch = history.changes.flatMap((change) => {
        const messages: SagaMessage[] = [];

        // Add initiating message if it exists and hasn't been fetched
        if (change.initiating_message && !fetchedMessages.value.has(change.initiating_message.message_id)) {
          messages.push(change.initiating_message);
        }

        // Add all unfetched outgoing messages
        if (change.outgoing_messages) {
          messages.push(...change.outgoing_messages.filter((msg) => !fetchedMessages.value.has(msg.message_id)));
        }
        return messages;
      });

      // Check if any messages need body_url
      const needsBodyUrl = messagesToFetch.every((msg) => !msg.body_url);
      if (needsBodyUrl && messagesToFetch.length > 0) {
        const auditMessages = await getAuditMessages(sagaId.value!);
        messagesToFetch.forEach((message) => {
          const auditMessage = auditMessages.find((x: Message) => x.message_id === message.message_id);
          if (auditMessage) {
            message.body_url = auditMessage.body_url;
          }
        });
      }

      // Fetch data for each unfetched message in parallel and store results
      const fetchPromises = messagesToFetch.map(async (message) => {
        const data = await fetchSagaMessageData(message);
        fetchedMessages.value.add(message.message_id);
        return data;
      });

      const newMessageData = await Promise.all(fetchPromises);
      // Add new message data to the existing array
      messagesData.value = [...messagesData.value, ...newMessageData];
    } catch (e) {
      error.value = e instanceof Error ? e.message : "Unknown error occurred";
    } finally {
      messageDataLoading.value = false;
    }
  }

  return {
    sagaHistory,
    sagaId,
    loading,
    messageDataLoading,
    error,
    showMessageData,
    messagesData,
    setSagaId,
    clearSagaHistory,
    toggleMessageData,
  };
});

if (import.meta.hot) {
  import.meta.hot.accept(acceptHMRUpdate(useSagaDiagramStore, import.meta.hot));
}

export type SagaDiagramStore = ReturnType<typeof useSagaDiagramStore>;
