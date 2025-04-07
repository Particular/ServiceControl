<script setup lang="ts">
import { Direction, RoutedMessageType } from "@/resources/SequenceDiagram/RoutedMessage";
import { computed, ref } from "vue";
import { useSequenceDiagramStore } from "@/stores/SequenceDiagramStore";
import { storeToRefs } from "pinia";

const Arrow_Head_Width = 4;
const Message_Type_Margin = 4;

const store = useSequenceDiagramStore();
const { routes, handlerLocations, highlightId } = storeToRefs(store);

const messageTypeRefs = ref<SVGTextElement[]>([]);

const arrows = computed(() =>
  routes.value.map((route, index) => {
    if (!route.name) return;
    const fromHandler = route.fromRoutedMessage?.fromHandler;
    if (!fromHandler) return;
    const fromHandlerLocation = handlerLocations.value.find((hl) => hl.id === fromHandler.id);
    if (!fromHandlerLocation) return;
    const toHandlerLocation = handlerLocations.value.find((hl) => hl.id === route.fromRoutedMessage?.toHandler?.id);
    if (!toHandlerLocation) return;

    //TODO: is messageId enough to uniquely identify?
    const arrowIndex = fromHandler.outMessages.findIndex((out) => route.fromRoutedMessage?.messageId === out.messageId) + 1;
    const y = fromHandlerLocation.y + (fromHandlerLocation.height / (fromHandler.outMessages.length + 1)) * arrowIndex; //TODO work out the reason - 15 is applied in WPF;

    const [direction, width, fromX] = (() => {
      if (fromHandlerLocation.id === toHandlerLocation.id) return [Direction.Right, 15 + Arrow_Head_Width, fromHandlerLocation.right];
      if (handlerLocations.value.indexOf(fromHandlerLocation) < handlerLocations.value.indexOf(toHandlerLocation)) return [Direction.Right, toHandlerLocation.left - fromHandlerLocation.right - Arrow_Head_Width, fromHandlerLocation.right];
      return [Direction.Left, toHandlerLocation.left - fromHandlerLocation.right - Arrow_Head_Width, toHandlerLocation.left];
    })();
    route.fromRoutedMessage.direction = direction;

    const toX = toHandlerLocation.left + (toHandlerLocation.right - toHandlerLocation.left) / 2;
    const messageTypeElement = messageTypeRefs.value[index];
    const messageTypeElementBounds = messageTypeElement?.getBBox();

    return {
      id: route.name,
      fromX,
      y,
      direction,
      width,
      toX,
      height: Math.abs(y - toHandlerLocation.y),
      type: route.fromRoutedMessage.type,
      messageType: route.fromRoutedMessage.name,
      messageTypeOffset: toX + 3, //TODO: apply using messageTypeRef if arrow is left
      highlight: highlightId.value === route.name,
      highlightTextWidth: messageTypeElementBounds?.width,
      highlightTextHeight: messageTypeElementBounds?.height,
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
        <path :d="`M${arrow.fromX} ${arrow.y} h${arrow.width}`" stroke-width="4" stroke="black" />
        <path v-if="arrow.direction === Direction.Right" :d="`M${arrow.fromX + arrow.width} ${arrow.y - 7.5} l10 7.5 -10,7.5z`" fill="black" />
        <path v-if="arrow.direction === Direction.Left" :d="`M${arrow.fromX - Arrow_Head_Width} ${arrow.y} l10,-7.5 0,15z`" fill="black" />
      </g>
      <!--Highlight Arrow-->
      <g v-if="arrow.highlight" :transform="`translate(${arrow.toX},${arrow.y})`" stroke="var(--highlight)" fill="var(--highlight)">
        <path :d="`M0 0 v${arrow.height - 6}`" stroke-width="2" />
        <path :d="`M0 ${arrow.height} l-3,-6 6,0z`" />
      </g>
      <!--Message Type and Icon-->
      <g
        class="clickable"
        :transform="`translate(${arrow.messageTypeOffset}, ${arrow.y - 7.5 - Message_Type_Margin})`"
        :fill="arrow.highlight ? 'var(--highlight)' : 'black'"
        @mouseover="() => store.setHighlightId(arrow.id)"
        @mouseleave="() => store.setHighlightId()"
      >
        <!--19 is width of MessageType icon, plus a gap-->
        <rect
          v-if="arrow.highlight && arrow.messageTypeOffset"
          :width="arrow.highlightTextWidth + 19 + Message_Type_Margin + Message_Type_Margin"
          :height="arrow.highlightTextHeight + Message_Type_Margin + Message_Type_Margin"
          fill="var(--highlight-background)"
        />
        <svg :x="Message_Type_Margin" :y="Message_Type_Margin" width="15" height="15" viewBox="0 0 32 32">
          <path
            v-if="arrow.type === RoutedMessageType.Timeout"
            d="M21.8 18.5H14V10h2.5v6h5.3L21.8 18.5L21.8 18.5z M25.1 24.4c2-2.2 3.2-5.1 3.2-8.3  c0-6.8-5.5-12.3-12.3-12.3C9.2 3.7 3.7 9.2 3.7 16.1c0 3.2 1.2 6.1 3.2 8.3c-0.6 1.5-1.4 3.6-2 4.9c-0.1 0.2 0 0.4 0.2 0.6  c0.2 0.1 0.4 0.2 0.6 0c1.3-0.8 3.1-2.1 4.5-3c1.7 0.9 3.7 1.5 5.9 1.5s4.1-0.5 5.9-1.5l4.5 3c0.2 0.1 0.4 0.1 0.6 0  c0.2-0.1 0.2-0.4 0.2-0.6L25.1 24.4z M16 25.3c-5.1 0-9.3-4.1-9.3-9.3c0-5.1 4.1-9.3 9.3-9.3c5.1 0 9.3 4.1 9.3 9.3  C25.3 21.2 21.1 25.3 16 25.3z M10.5 2.8C9.6 2.3 8.6 2 7.6 2C4.5 2 2 4.5 2 7.6c0 1 0.3 2 0.8 2.8C4.2 7 7 4.2 10.5 2.8z   M29.2 10.4C29.7 9.6 30 8.6 30 7.6C30 4.5 27.5 2 24.4 2c-1 0-2 0.3-2.9 0.8C25 4.2 27.8 7 29.2 10.4z"
          />
          <path
            v-else-if="arrow.type === RoutedMessageType.Event"
            d="M 0,0 M 32,32 M 0,16 A 6,6 0 1 1 12,16 A 6,6 0 1 1 0,16   M 14,13 v6 h10 v2 L32,16 L24,11 v2   M13.78,19.54 L9.54,23.78 L16.61,30.85 L15.19,32.26 L24.38,34.38 L22.26,25.19 L20.85,26.61   M9.54,8.22 L13.78,12.46 L20.85,5.39 L22.26,6.81 L24.38,-2.38 L15.19,-0.26 L16.61,1.15"
          />
          <path v-else-if="arrow.type === RoutedMessageType.Command" d="M 0,0 M 32,32 M 0,16 A 6,6 0 1 1 12,16 A 6,6 0 1 1 0,16   M 14,13 v6 h10 v2 L32,16 L24,11 v2 z" />
          <path v-else-if="arrow.type === RoutedMessageType.Local" d="M17-1h-7v2h5v7H9V5.8L3,9l6,3.2V10h8V-1z M9,0.1C9,1.7,7.7,3,6,3S3,1.7,3,0.1S4.3-3,6-3S9-1.6,9,0.1z" />
        </svg>
        <text :x="15 + Message_Type_Margin + Message_Type_Margin" :y="Message_Type_Margin" alignment-baseline="before-edge" :ref="(el) => setMessageTypeRef(el as SVGTextElement, i)">{{ arrow.messageType }}</text>
      </g>
    </g>
  </template>
</template>

<style scoped>
.clickable {
  cursor: pointer;
}
</style>
