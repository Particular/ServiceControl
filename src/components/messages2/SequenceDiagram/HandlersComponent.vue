<script setup lang="ts">
import { HandlerState } from "@/resources/SequenceDiagram/Handler";
import { computed, onActivated, ref, watch } from "vue";
import { Direction } from "@/resources/SequenceDiagram/RoutedMessage";
import { useSequenceDiagramStore } from "@/stores/SequenceDiagramStore";
import { storeToRefs } from "pinia";

const Height_Per_Out = 40;
const Handler_Gap = 20;
const Handler_Width = 14;

const store = useSequenceDiagramStore();
const { handlers, endpointCentrePoints, highlightId, selectedId } = storeToRefs(store);

const messageTypeRefs = ref<SVGTextElement[]>([]);
const hasMadeVisible = ref(false);
const selectedElement = ref<SVGElement>();
//reset values to allow scroll to element on switching back to this tab
onActivated(() => {
  hasMadeVisible.value = false;
  if (selectedElement.value) scrollToIfSelected(selectedElement.value, selectedId.value);
});
watch(selectedId, () => (selectedElement.value = undefined));

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
      if (handler.route && handler.route.name === selectedId.value) return "var(--highlight)";
      if (handler.route && handler.route.name === highlightId.value) return "var(--highlight-background)";
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

    //determine which side of the handler to render the messageType on. If it's the left side (for a right arrow) then we apply a negative offset
    const messageTypeOffset = handler.direction === Direction.Right ? ((messageTypeElement?.getBBox().width ?? 0) + 24) * -1 : 20;
    const left = (endpoint?.centre ?? 0) - Handler_Width / 2;
    const right = (endpoint?.centre ?? 0) + Handler_Width / 2;
    if (left + messageTypeOffset < 0) {
      store.setStartX(-1 * (left + messageTypeOffset) + 20);
    }

    return {
      id: handler.id,
      messageId: { id: handler.selectedMessage?.message_id, uniqueId: handler.selectedMessage?.id },
      isError: handler.state === HandlerState.Fail,
      endpointName: handler.endpoint.name,
      incomingId: handler.route?.name,
      left,
      right,
      y,
      height,
      fill,
      icon,
      iconSize,
      messageType: handler.friendlyName,
      messageTypeOffset,
      messageTypeHighlight: handler.route?.name === highlightId.value,
      messageTypeSelected: handler.route?.name === selectedId.value,
      setUIRef: (el: SVGElement) => (handler.uiRef = el),
    };
  });

  store.setMaxHeight(nextY);
  store.setHandlerLocations(result.map((handler) => ({ id: handler.id, endpointName: handler.endpointName, left: handler.left, right: handler.right, y: handler.y, height: handler.height })));

  return result;
});

function setMessageTypeRef(el: SVGTextElement, index: number) {
  if (el) messageTypeRefs.value[index] = el;
}

function scrollToIfSelected(el: SVGElement, handlerId: string | undefined) {
  if (!hasMadeVisible.value && el && handlerId === selectedId.value) {
    hasMadeVisible.value = true;
    selectedElement.value = el;
    //can't be done immediately since the sequence diagram hasn't completed layout yet
    setTimeout(() => selectedElement.value!.scrollIntoView(false), 30);
  }
}
</script>

<template>
  <g v-for="(handler, i) in handlerItems" :key="`${handler.id}###${handler.endpointName}`" :ref="(el) => scrollToIfSelected(el as SVGElement, handler.incomingId)" :transform="`translate(${handler.left}, ${handler.y})`">
    <!--Handler Activation Box-->
    <g :ref="(el) => handler.setUIRef(el as SVGElement)" class="activation-box">
      <rect
        :width="Handler_Width"
        :height="handler.height"
        :class="{
          clickable: handler.incomingId && !handler.messageTypeSelected,
        }"
        :fill="handler.fill"
        @mouseover="() => store.setHighlightId(handler.incomingId)"
        @mouseleave="() => store.setHighlightId()"
        @click="handler.incomingId && !handler.messageTypeSelected && store.navigateTo(handler.messageId.uniqueId, handler.messageId.id, handler.isError)"
      />
      <path v-if="handler.icon" :d="handler.icon" fill="white" :transform="`translate(${Handler_Width / 2 - handler.iconSize / 2}, ${handler.height / 2 - handler.iconSize / 2})`" />
    </g>
    <!--Message Type and Icon-->
    <g
      v-if="handler.messageType"
      :transform="`translate(${handler.messageTypeOffset}, 4)`"
      :class="{
        clickable: !handler.messageTypeSelected,
        'message-type': true,
        highlight: handler.messageTypeHighlight || handler.messageTypeSelected,
      }"
      @mouseover="() => store.setHighlightId(handler.incomingId)"
      @mouseleave="() => store.setHighlightId()"
      @click="handler.incomingId && !handler.messageTypeSelected && store.navigateTo(handler.messageId.uniqueId, handler.messageId.id, handler.isError)"
    >
      <path d="M9,3L9,3 9,0 0,0 0,3 4,3 4,6 0,6 0,9 4,9 4,12 0,12 0,15 9,15 9,12 5,12 5,9 9,9 9,6 5,6 5,3z" />
      <text x="14" y="0" dominant-baseline="text-before-edge" :ref="(el) => setMessageTypeRef(el as SVGTextElement, i)">{{ handler.messageType }}</text>
    </g>
  </g>
</template>

<style scoped>
.clickable {
  cursor: pointer;
}

.activation-box:focus {
  outline: none;
}

.message-type {
  fill: var(--gray40);
}

.message-type.highlight {
  fill: var(--highlight);
}
</style>
