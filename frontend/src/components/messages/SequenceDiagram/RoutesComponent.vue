<script setup lang="ts">
import { Direction, RoutedMessageType } from "@/resources/SequenceDiagram/RoutedMessage";
import { computed, ref } from "vue";
import { useSequenceDiagramStore } from "@/stores/SequenceDiagramStore";
import { storeToRefs } from "pinia";
import { HandlerState } from "@/resources/SequenceDiagram/Handler";

const Arrow_Head_Width = 10;
const Message_Type_Margin = 4;

const store = useSequenceDiagramStore();
const { selectedId, routes, handlerLocations, highlightId } = storeToRefs(store);

const messageTypeRefs = ref<SVGTextElement[]>([]);

const arrows = computed(() =>
  routes.value.map((route, index) => {
    if (!route.name) return;
    const fromHandler = route.fromRoutedMessage?.fromHandler;
    if (!fromHandler) return;
    const fromHandlerLocation = handlerLocations.value.find((hl) => hl.id === fromHandler.id && hl.endpointName === fromHandler.endpoint.name);
    if (!fromHandlerLocation) return;
    const toHandlerLocation = handlerLocations.value.find((hl) => hl.id === route.fromRoutedMessage?.toHandler?.id && hl.endpointName === route.fromRoutedMessage?.receiving.name);
    if (!toHandlerLocation) return;

    const messageTypeElement = messageTypeRefs.value[index];
    const messageTypeElementBounds = messageTypeElement?.getBBox();
    const arrowIndex = fromHandler.outMessages.findIndex((out) => route.fromRoutedMessage?.messageId === out.messageId && route.fromRoutedMessage?.receiving.name === out.receiving.name) + 1;
    const y = fromHandlerLocation.y + (fromHandlerLocation.height / (fromHandler.outMessages.length + 1)) * arrowIndex; //TODO work out the reason - 15 is applied in WPF;

    const toHandlerCentre = toHandlerLocation.left + (toHandlerLocation.right - toHandlerLocation.left) / 2;
    const [direction, width, fromX, messageTypeOffset] = (() => {
      if (fromHandlerLocation.left === toHandlerLocation.left) return [Direction.Right, 15 + Arrow_Head_Width, fromHandlerLocation.right, toHandlerCentre + 45];
      if (fromHandlerLocation.left < toHandlerLocation.left) return [Direction.Right, toHandlerCentre - fromHandlerLocation.right - Arrow_Head_Width - 1, fromHandlerLocation.right, toHandlerCentre + 3];
      return [Direction.Left, toHandlerCentre - fromHandlerLocation.left + Arrow_Head_Width + 1, fromHandlerLocation.left, toHandlerCentre - ((messageTypeElementBounds?.width ?? 0) + 15 + Message_Type_Margin * 3 + 3)];
    })();
    route.fromRoutedMessage.direction = direction;

    if (messageTypeOffset < 0) {
      store.setStartX(-1 * messageTypeOffset + 20);
    }

    return {
      id: route.name,
      selected: route.name === selectedId.value,
      messageId: { uniqueId: route.fromRoutedMessage.selectedMessage.id, id: route.fromRoutedMessage.selectedMessage.message_id },
      isHandlerError: route.processingHandler?.state === HandlerState.Fail,
      fromX,
      y,
      direction,
      width,
      toHandlerCentre,
      height: Math.abs(y - toHandlerLocation.y),
      type: route.fromRoutedMessage.type,
      messageType: route.fromRoutedMessage.name,
      messageTypeOffset,
      highlight: highlightId.value === route.name,
      highlightTextWidth: messageTypeElementBounds?.width,
      highlightTextHeight: messageTypeElementBounds?.height,
      setUIRef: (el: SVGElement) => (route.uiRef = el),
    };
  })
);

function setMessageTypeRef(el: SVGTextElement, index: number) {
  if (el) messageTypeRefs.value[index] = el;
}
</script>

<template>
  <template v-for="(arrow, i) in arrows" :key="arrow?.id">
    <g v-if="arrow != null">
      <!--Main Arrow-->
      <g>
        <path :d="`M${arrow.fromX} ${arrow.y} h${arrow.width}`" stroke-width="3.5" stroke="black" :stroke-dasharray="arrow.type === RoutedMessageType.Event ? '12 8' : undefined" />
        <path v-if="arrow.direction === Direction.Right" :d="`M${arrow.fromX + arrow.width} ${arrow.y - 7.5} l10 7.5 -10,7.5z`" fill="black" />
        <path v-if="arrow.direction === Direction.Left" :d="`M${arrow.toHandlerCentre + 1} ${arrow.y} l10,-7.5 0,15z`" fill="black" />
      </g>
      <!--Highlight Arrow-->
      <g v-if="arrow.highlight || arrow.selected" :transform="`translate(${arrow.toHandlerCentre},${arrow.y})`" stroke="var(--highlight)" fill="var(--highlight)">
        <path :d="`M0 0 v${arrow.height - 6}`" stroke-width="2" />
        <path :d="`M0 ${arrow.height} l-3,-6 6,0z`" />
      </g>
      <!--Message Type and Icon-->
      <g
        :class="{
          clickable: !arrow.selected,
          'message-type': true,
          highlight: arrow.highlight,
          selected: arrow.selected,
        }"
        :transform="`translate(${arrow.messageTypeOffset}, ${arrow.y - 7.5 - Message_Type_Margin})`"
        @mouseover="() => store.setHighlightId(arrow.id)"
        @mouseleave="() => store.setHighlightId()"
        @click="!arrow.selected && store.navigateTo(arrow.messageId.uniqueId, arrow.messageId.id, arrow.isHandlerError)"
        :ref="(el) => arrow.setUIRef(el as SVGElement)"
      >
        <!--19 is width of MessageType icon, plus a gap-->
        <rect
          v-if="(arrow.highlight || arrow.selected) && arrow.messageTypeOffset"
          :width="(arrow.highlightTextWidth ?? 0) + 19 + Message_Type_Margin + Message_Type_Margin"
          :height="(arrow.highlightTextHeight ?? 0) + Message_Type_Margin + Message_Type_Margin"
          class="border"
        />
        <svg :x="Message_Type_Margin" :y="Message_Type_Margin" width="15" height="15" viewBox="0 0 32 32">
          <path
            v-if="arrow.type === RoutedMessageType.Timeout"
            d="M21.8 18.5H14V10h2.5v6h5.3L21.8 18.5L21.8 18.5z M25.1 24.4c2-2.2 3.2-5.1 3.2-8.3  c0-6.8-5.5-12.3-12.3-12.3C9.2 3.7 3.7 9.2 3.7 16.1c0 3.2 1.2 6.1 3.2 8.3c-0.6 1.5-1.4 3.6-2 4.9c-0.1 0.2 0 0.4 0.2 0.6  c0.2 0.1 0.4 0.2 0.6 0c1.3-0.8 3.1-2.1 4.5-3c1.7 0.9 3.7 1.5 5.9 1.5s4.1-0.5 5.9-1.5l4.5 3c0.2 0.1 0.4 0.1 0.6 0  c0.2-0.1 0.2-0.4 0.2-0.6L25.1 24.4z M16 25.3c-5.1 0-9.3-4.1-9.3-9.3c0-5.1 4.1-9.3 9.3-9.3c5.1 0 9.3 4.1 9.3 9.3  C25.3 21.2 21.1 25.3 16 25.3z M10.5 2.8C9.6 2.3 8.6 2 7.6 2C4.5 2 2 4.5 2 7.6c0 1 0.3 2 0.8 2.8C4.2 7 7 4.2 10.5 2.8z   M29.2 10.4C29.7 9.6 30 8.6 30 7.6C30 4.5 27.5 2 24.4 2c-1 0-2 0.3-2.9 0.8C25 4.2 27.8 7 29.2 10.4z"
          />
          <path
            v-else-if="arrow.type === RoutedMessageType.Event"
            d="M 0 2 M 27.8 29.8 M 0 15.9 A 5.2 5.2 90 1 1 10.4 15.9 A 5.2 5.2 90 1 1 0 15.9 M 12.1 13.3 v 5.2 h 8.7 v 1.8 L 27.8 15.9 L 20.8 11.6 v 1.8 M 11.9 19 L 8.3 22.6 L 14.3 28.8 L 13.1 30 L 21.2 31.9 L 19.3 23.9 L 18.1 25.1 M 8.3 9.1 L 11.9 12.9 L 18.1 6.7 L 19.3 7.9 L 21.2 0 L 13.1 1.9 L 14.3 3.1"
          />
          <path v-else-if="arrow.type === RoutedMessageType.Command" d="M 0,0 M 32,32 M 0,16 A 6,6 0 1 1 12,16 A 6,6 0 1 1 0,16   M 14,13 v6 h10 v2 L32,16 L24,11 v2 z" />
          <path v-else-if="arrow.type === RoutedMessageType.Local" d="M 32 6 h -14 v 4 h 10 v 14 H 16 V 19.6 L 4 26 l 12 6.4 V 28 h 16 V 6 z M 16 8.2 C 16 11.4 13.4 14 10 14 S 4 11.4 4 8.2 S 6.6 2 10 2 S 16 4.8 16 8 z" />
        </svg>
        <text :x="15 + Message_Type_Margin + Message_Type_Margin" :y="Message_Type_Margin" dominant-baseline="text-before-edge" :ref="(el) => setMessageTypeRef(el as SVGTextElement, i)">{{ arrow.messageType }}</text>
      </g>
    </g>
  </template>
</template>

<style scoped>
.clickable {
  cursor: pointer;
}

.message-type {
  fill: black;
  outline: none;
}

.message-type.selected {
  fill: white;
}

.message-type .border {
  fill: var(--highlight-background);
}

.message-type.selected .border {
  fill: var(--highlight);
}

.message-type:not(.selected).highlight {
  fill: var(--highlight);
}

.message-type text::selection {
  fill: white;
}

.message-type.selected text::selection {
  background-color: black;
}
</style>
