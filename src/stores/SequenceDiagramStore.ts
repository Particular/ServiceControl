import { acceptHMRUpdate, defineStore, storeToRefs } from "pinia";
import { computed, ref, watch } from "vue";
import { ModelCreator } from "@/resources/SequenceDiagram/SequenceModel";
import { Endpoint } from "@/resources/SequenceDiagram/Endpoint";
import { Handler } from "@/resources/SequenceDiagram/Handler";
import { MessageProcessingRoute } from "@/resources/SequenceDiagram/RoutedMessage";
import { useMessageStore } from "./MessageStore";
import { useRouter } from "vue-router";
import routeLinks from "@/router/routeLinks";

export interface EndpointCentrePoint {
  name: string;
  centre?: number;
  top: number;
}

export interface HandlerLocation {
  id: string;
  endpointName: string;
  left: number;
  right: number;
  y: number;
  height: number;
}

export const Endpoint_Width = 260;

export const useSequenceDiagramStore = defineStore("SequenceDiagramStore", () => {
  const messageStore = useMessageStore();
  const { state } = storeToRefs(messageStore);
  const router = useRouter();

  const startX = ref(Endpoint_Width / 2);
  const endpoints = ref<Endpoint[]>([]);
  const handlers = ref<Handler[]>([]);
  const routes = ref<MessageProcessingRoute[]>([]);
  const endpointCentrePoints = ref<EndpointCentrePoint[]>([]);
  const maxWidth = ref(150);
  const maxHeight = ref(150);
  const handlerLocations = ref<HandlerLocation[]>([]);
  const highlightId = ref<string>();

  const selectedId = computed(() => `${state.value.data.message_type ?? ""}(${state.value.data.id})`);

  watch(
    () => messageStore.conversationData.data,
    (conversationData) => {
      if (conversationData.length) {
        startX.value = Endpoint_Width / 2;
        const model = new ModelCreator(conversationData);
        endpoints.value = model.endpoints;
        handlers.value = model.handlers;
        routes.value = model.routes;
      }
    },
    { immediate: true }
  );

  function setStartX(offset: number) {
    const newValue = Math.max(offset + Endpoint_Width / 2, startX.value);
    if (newValue === startX.value) return;
    startX.value = newValue;
  }

  function setMaxWidth(width: number) {
    maxWidth.value = width;
  }

  function setMaxHeight(height: number) {
    maxHeight.value = height;
  }

  function setEndpointCentrePoints(centrePoints: EndpointCentrePoint[]) {
    endpointCentrePoints.value = centrePoints;
  }

  function setHandlerLocations(locations: HandlerLocation[]) {
    handlerLocations.value = locations;
  }

  function setHighlightId(id?: string) {
    highlightId.value = id;
  }

  function refreshConversation() {
    if (messageStore.state.data.conversation_id) messageStore.loadConversation(messageStore.state.data.conversation_id);
  }

  function navigateTo(messageUniqueId: string | undefined, messageId: string | undefined, isError: boolean) {
    if (messageUniqueId == null) return;
    if (!isError && messageId == null) return;

    router.push({ path: isError ? routeLinks.messages.failedMessage.link(messageUniqueId) : routeLinks.messages.successMessage.link(messageId!, messageUniqueId) });
  }

  return {
    startX,
    endpoints,
    handlers,
    routes,
    endpointCentrePoints,
    maxWidth,
    maxHeight,
    handlerLocations,
    highlightId,
    selectedId,
    setStartX,
    setMaxWidth,
    setMaxHeight,
    setEndpointCentrePoints,
    setHandlerLocations,
    setHighlightId,
    refreshConversation,
    navigateTo,
  };
});

if (import.meta.hot) {
  import.meta.hot.accept(acceptHMRUpdate(useSequenceDiagramStore, import.meta.hot));
}

export type SequenceDiagramStore = ReturnType<typeof useSequenceDiagramStore>;
