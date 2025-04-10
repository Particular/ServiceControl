import { useSequenceDiagramStore } from "@/stores/SequenceDiagramStore";
import { storeToRefs } from "pinia";
import { h, watch } from "vue";
import { useTippy } from "vue-tippy";
import EndpointTooltip from "./EndpointTooltip.vue";
import HandlerTooltip from "./HandlerTooltip.vue";
import RouteTooltip from "./RouteTooltip.vue";

export default function useTooltips() {
  const store = useSequenceDiagramStore();
  const { endpoints, handlers, routes } = storeToRefs(store);

  watch(
    () => endpoints.value.map((endpoint) => endpoint.uiRef),
    () =>
      endpoints.value
        .filter((endpoint) => endpoint.uiRef)
        .forEach((endpoint) =>
          useTippy(endpoint.uiRef, {
            interactive: true,
            appendTo: () => document.body,
            content: h(EndpointTooltip, { endpoint }),
            placement: "bottom",
            delay: [800, null],
          })
        )
  );

  watch(
    () => handlers.value.map((handler) => handler.uiRef),
    () =>
      handlers.value
        .filter((handler) => handler.uiRef)
        .forEach((handler) =>
          useTippy(handler.uiRef, {
            interactive: true,
            appendTo: () => document.body,
            content: h(HandlerTooltip, { handler }),
            delay: [800, null],
          })
        )
  );

  watch(
    () => routes.value.map((route) => route.uiRef),
    () =>
      routes.value
        .filter((route) => route.uiRef && route.fromRoutedMessage)
        .forEach((route) =>
          useTippy(route.uiRef, {
            interactive: true,
            appendTo: () => document.body,
            content: h(RouteTooltip, { routedMessage: route.fromRoutedMessage! }),
            delay: [800, null],
            maxWidth: 400,
          })
        )
  );
}
