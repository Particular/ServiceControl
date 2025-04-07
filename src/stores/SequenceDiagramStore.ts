import { useFetchFromServiceControl } from "@/composables/serviceServiceControlUrls";
import { acceptHMRUpdate, defineStore } from "pinia";
import { ref, watch } from "vue";
import { ModelCreator } from "@/resources/SequenceDiagram/SequenceModel";
import Message from "@/resources/Message";
import { Endpoint } from "@/resources/SequenceDiagram/Endpoint";
import { Handler } from "@/resources/SequenceDiagram/Handler";
import { MessageProcessingRoute } from "@/resources/SequenceDiagram/RoutedMessage";

export interface EndpointCentrePoint {
  name: string;
  centre: number;
  top: number;
}

export interface HandlerLocation {
  id: string;
  left: number;
  right: number;
  y: number;
  height: number;
}

export const useSequenceDiagramStore = defineStore("SequenceDiagramStore", () => {
  const conversationId = ref<string>();

  const endpoints = ref<Endpoint[]>([]);
  const handlers = ref<Handler[]>([]);
  const routes = ref<MessageProcessingRoute[]>([]);
  const endpointCentrePoints = ref<EndpointCentrePoint[]>([]);
  const maxWidth = ref(150);
  const maxHeight = ref(150);
  const handlerLocations = ref<HandlerLocation[]>([]);
  const highlightId = ref<string>();

  watch(conversationId, async () => {
    if (!conversationId.value) return;
    const response = await useFetchFromServiceControl(`conversations/${conversationId.value}`);
    if (response.status === 404) {
      return;
    }

    const model = new ModelCreator((await response.json()) as Message[]);
    endpoints.value = model.endpoints;
    handlers.value = model.handlers;
    routes.value = model.routes;
  });

  function setConversationId(id: string) {
    conversationId.value = id;
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

  return {
    setConversationId,
    endpoints,
    handlers,
    routes,
    endpointCentrePoints,
    maxWidth,
    maxHeight,
    handlerLocations,
    highlightId,
    setMaxWidth,
    setMaxHeight,
    setEndpointCentrePoints,
    setHandlerLocations,
    setHighlightId,
  };
});

if (import.meta.hot) {
  import.meta.hot.accept(acceptHMRUpdate(useSequenceDiagramStore, import.meta.hot));
}

export type SequenceDiagramStore = ReturnType<typeof useSequenceDiagramStore>;
