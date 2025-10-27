import { acceptHMRUpdate, defineStore } from "pinia";
import { ref, watch } from "vue";
import { SagaHistory, SagaMessage } from "@/resources/SagaHistory";
import Message from "@/resources/Message";
import { parse, stringify } from "lossless-json";
import xmlFormat from "xml-formatter";
import { DataContainer } from "./DataContainer";
import { useMessageStore } from "./MessageStore";
import { useServiceControlStore } from "./ServiceControlStore";

export interface SagaMessageData {
  message_id: string;
  body: DataContainer<{ value?: string; content_type?: string; no_content?: boolean }>;
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
  const selectedMessageId = ref<string | null>(null);
  const scrollToTimeoutRequest = ref(false);
  const scrollToTimeout = ref(false);
  const MessageBodyEndpoint = "messages/{0}/body";
  const messageStore = useMessageStore();
  const serviceControlStore = useServiceControlStore();

  watch(
    () => messageStore.state.data.message_id,
    (newMessageId) => {
      if (newMessageId) {
        setSelectedMessageId(newMessageId);
      }
    },
    { immediate: true }
  );

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
      const response = await serviceControlStore.fetchFromServiceControl(`sagas/${id}`);

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

  async function fetchSagaMessageData(message: SagaMessage): Promise<SagaMessageData> {
    const bodyUrl = (message.body_url ?? formatUrl(MessageBodyEndpoint, message.message_id)).replace(/^\//, "");
    const result: SagaMessageData = {
      message_id: message.message_id,
      body: { data: {} },
    };

    result.body.loading = true;
    result.body.failed_to_load = false;

    try {
      const response = await serviceControlStore.fetchFromServiceControl(bodyUrl);
      if (response.status === 404) {
        result.body.not_found = true;
        return result;
      }

      if (response.status === 204) {
        result.body.data.no_content = true;
        return result;
      }

      const contentType = response.headers.get("content-type");
      result.body.data.content_type = contentType ?? "text/plain";
      result.body.data.value = await response.text();

      if (contentType === "application/json" && result.body.data.value) {
        // Only format non-empty JSON objects
        result.body.data.value = result.body.data.value !== "{}" ? (stringify(parse(result.body.data.value), null, 2) ?? result.body.data.value) : "";
      } else if (contentType === "text/xml" && result.body.data.value) {
        // Format XML if it has content in the root element
        const xmlRootElement = getContentOfXmlRootElement(result.body.data.value);
        result.body.data.value = xmlRootElement ? xmlFormat(result.body.data.value, { indentation: "  ", collapseContent: true }) : "";
      }
    } catch {
      result.body.failed_to_load = true;
    } finally {
      result.body.loading = false;
    }

    return result;
  }

  function getContentOfXmlRootElement(xml: string): string {
    const parser = new DOMParser();
    const doc = parser.parseFromString(xml, "text/xml");
    const rootElement = doc.documentElement;
    if (rootElement) {
      const rootElementText = rootElement.textContent;
      if (rootElementText) {
        return rootElementText;
      }
    }
    return "";
  }

  async function getAuditMessages(sagaId: string) {
    try {
      const response = await serviceControlStore.fetchFromServiceControl(`messages/search?q=${sagaId}`);
      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }
      return await response.json();
    } catch (error) {
      console.error("Error fetching audit messages:", error);
      return { result: [] };
    }
  }

  function clearSagaHistory() {
    sagaHistory.value = null;
    sagaId.value = null;
    error.value = null;
    fetchedMessages.value.clear();
    messagesData.value = [];
    selectedMessageId.value = null;
    scrollToTimeoutRequest.value = false;
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

  function setSelectedMessageId(messageId: string | null) {
    selectedMessageId.value = messageId;
  }

  return {
    sagaHistory,
    sagaId,
    loading,
    messageDataLoading,
    error,
    showMessageData,
    messagesData,
    selectedMessageId,
    scrollToTimeoutRequest,
    scrollToTimeout,
    setSagaId,
    clearSagaHistory,
    toggleMessageData,
    setSelectedMessageId,
  };
});

if (import.meta.hot) {
  import.meta.hot.accept(acceptHMRUpdate(useSagaDiagramStore, import.meta.hot));
}

export type SagaDiagramStore = ReturnType<typeof useSagaDiagramStore>;
