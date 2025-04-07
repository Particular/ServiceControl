<script setup lang="ts">
import { HandlerState } from "@/resources/SequenceDiagram/Handler";
import { computed, ref } from "vue";
import { Direction } from "@/resources/SequenceDiagram/RoutedMessage";
import { useSequenceDiagramStore } from "@/stores/SequenceDiagramStore";
import { storeToRefs } from "pinia";

const Height_Per_Out = 40;
const Handler_Gap = 20;
const Handler_Width = 14;

const store = useSequenceDiagramStore();
const { handlers, endpointCentrePoints, highlightId } = storeToRefs(store);

const messageTypeRefs = ref<SVGTextElement[]>([]);

const handlerItems = computed(() => {
  let nextY = 0;
  const result = handlers.value.map((handler, index) => {
    const endpoint = endpointCentrePoints.value.find((cp) => cp.name === handler.endpoint.name)!;
    const messageTypeElement = messageTypeRefs.value[index];
    const count = handler.outMessages.length;
    const height = (count === 0 ? 1 : count) * Height_Per_Out;
    if (nextY === 0) nextY += Handler_Gap + (endpoint?.top ?? 0);
    const y = nextY;
    nextY += height + Handler_Gap;
    const fill = (() => {
      if (handler.id === "First") return "black";
      if (handler.state === HandlerState.Fail) return "var(--error)";
      if (handler.route?.name === highlightId.value) return "var(--highlight-background)";
      return "var(--gray60)";
    })();
    const icon = (() => {
      if (handler.id === "First") return "M0,0L8,4 0,8z";
      if (handler.state === HandlerState.Fail) return "M6,0L0,6 6,12 12,6 6,0z M7,9L5,9 5,8 7,8 7,9z M5,7L5,3 7,3 7,7 5,7z";
      return null;
    })();
    const iconSize = (() => {
      if (handler.id === "First") return 8;
      if (handler.state === HandlerState.Fail) return 12;
      return 0;
    })();

    return {
      id: handler.id,
      incomingId: handler.route?.name,
      left: (endpoint?.centre ?? 0) - Handler_Width / 2,
      right: (endpoint?.centre ?? 0) + Handler_Width / 2,
      y,
      height,
      fill,
      icon,
      iconSize,
      messageType: handler.name,
      messageTypeOffset: handler.direction === Direction.Right ? ((messageTypeElement?.getBBox().width ?? 0) + 24) * -1 : 20,
      messageTypeHighlight: handler.route?.name === highlightId.value,
    };
  });

  store.setMaxHeight(nextY);
  store.setHandlerLocations(result.map((handler) => ({ id: handler.id, left: handler.left, right: handler.right, y: handler.y, height: handler.height })));

  return result;
});

function setMessageTypeRef(el: SVGTextElement, index: number) {
  if (el) messageTypeRefs.value[index] = el;
}
</script>

<template>
  <g v-for="(handler, i) in handlerItems" :key="handler.id" :transform="`translate(${handler.left}, ${handler.y})`">
    <!--Handler Activation Box-->
    <rect :width="Handler_Width" :height="handler.height" :class="handler.incomingId && 'clickable'" :fill="handler.fill" @mouseover="() => store.setHighlightId(handler.incomingId)" @mouseleave="() => store.setHighlightId()" />
    <path v-if="handler.icon" :d="handler.icon" fill="white" :transform="`translate(${Handler_Width / 2 - handler.iconSize / 2}, ${handler.height / 2 - handler.iconSize / 2})`" />
    <!--Message Type and Icon-->
    <g
      v-if="handler.messageType"
      :transform="`translate(${handler.messageTypeOffset}, 4)`"
      class="clickable"
      :fill="handler.messageTypeHighlight ? 'var(--highlight)' : 'var(--gray40)'"
      @mouseover="() => store.setHighlightId(handler.incomingId)"
      @mouseleave="() => store.setHighlightId()"
    >
      <path d="M9,3L9,3 9,0 0,0 0,3 4,3 4,6 0,6 0,9 4,9 4,12 0,12 0,15 9,15 9,12 5,12 5,9 9,9 9,6 5,6 5,3z" />
      <text x="14" y="10" alignment-baseline="middle" :ref="(el) => setMessageTypeRef(el as SVGTextElement, i)">{{ handler.messageType }}</text>
    </g>
  </g>
</template>

<style scoped>
.clickable {
  cursor: pointer;
}
</style>
