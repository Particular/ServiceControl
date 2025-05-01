<script setup lang="ts">
import { Endpoint } from "@/resources/SequenceDiagram/Endpoint";
import { Endpoint_Width, EndpointCentrePoint, useSequenceDiagramStore } from "@/stores/SequenceDiagramStore";
import { storeToRefs } from "pinia";
import { computed, ref, watch } from "vue";

interface EndpointWithLocation extends Endpoint {
  width: number;
  textWidth: number;
  x?: number;
  surround?: EndpointSurround;
}

interface EndpointSurround {
  x: number;
  y: number;
  width: number;
  height: number;
  fill: string;
  rx: string;
  strokeWidth: string;
  stroke: string;
}

const Endpoint_Gap = 30;
const Endpoint_Image_Width = 20;

defineProps<{
  yOffset: number;
}>();

const store = useSequenceDiagramStore();
const { startX, endpoints } = storeToRefs(store);

const epTextRefs = ref<SVGTextElement[]>([]);
const endpointItems = computed(() =>
  endpoints.value.map((x, index) => {
    const endpoint = x as EndpointWithLocation;
    const el = epTextRefs.value[index];
    if (el) {
      const bounds = el.getBBox();
      const previousEndpoint = index > 0 ? endpointItems.value[index - 1] : undefined;
      endpoint.width = Math.max(Endpoint_Width, bounds.width);
      endpoint.textWidth = bounds.width;
      endpoint.x = (previousEndpoint?.x ?? startX.value) + (previousEndpoint?.width ?? 0) + Endpoint_Gap;

      if (!endpoint.surround && el.isConnected) {
        const style = getComputedStyle(el);
        const padding_top = parseInt(style.getPropertyValue("padding-top"));
        const padding_left = parseInt(style.getPropertyValue("padding-left"));
        const padding_right = parseInt(style.getPropertyValue("padding-right"));
        const padding_bottom = parseInt(style.getPropertyValue("padding-bottom"));
        endpoint.surround = {
          x: endpoint.x - endpoint.width / 2 - padding_left,
          y: bounds.y - padding_top,
          width: endpoint.width + padding_left + padding_right,
          height: bounds.height + padding_top + padding_bottom,
          fill: style.getPropertyValue("background-color"),
          rx: style.getPropertyValue("border-radius"),
          strokeWidth: style.getPropertyValue("border-top-width"),
          stroke: style.getPropertyValue("border-top-color"),
        };
      }
    }
    return endpoint;
  })
);

watch(endpointItems, () => {
  store.setEndpointCentrePoints(endpointItems.value.map((endpoint) => ({ name: endpoint.name, centre: endpoint.x, top: (endpoint.surround?.y ?? 0) + (endpoint.surround?.height ?? 0) + 15 }) as EndpointCentrePoint));
  const lastEndpoint = endpointItems.value[endpointItems.value.length - 1];
  store.setMaxWidth((lastEndpoint.x ?? 0) + lastEndpoint.width);
});

watch(startX, () => {
  epTextRefs.value = [];
  endpoints.value.forEach((endpoint) => ((endpoint as EndpointWithLocation).surround = undefined));
});

function setEndpointTextRef(el: SVGTextElement, index: number) {
  if (el) epTextRefs.value[index] = el;
}
</script>

<template>
  <g v-for="(endpoint, i) in endpointItems" :key="endpoint.name" :transform="`translate(0,${yOffset + 15})`" :ref="(el) => (endpoint.uiRef = el as SVGElement)">
    <rect
      v-if="endpoint.surround"
      :x="endpoint.surround.x"
      :y="endpoint.surround.y"
      :width="endpoint.surround.width"
      :height="endpoint.surround.height"
      :fill="endpoint.surround.fill"
      :rx="endpoint.surround.rx"
      :stroke-width="endpoint.surround.strokeWidth"
      :stroke="endpoint.surround.stroke"
    ></rect>
    <g :transform="`translate(${(endpoint.x ?? Endpoint_Width / 2) - ((endpoint.textWidth ?? 0) + Endpoint_Image_Width) / 2}, 0)`">
      <path fill="var(--gray40)" d="M 0,0 M 18,18 M 0,2 v 14 h 14 v -4 h -6 v -6 h 6 v -4 h -14 M 9,7 v 4 h 9 v -4"></path>
      <text :x="Endpoint_Image_Width" y="10" alignment-baseline="middle" text-anchor="start" :ref="(el) => setEndpointTextRef(el as SVGTextElement, i)">{{ endpoint.name }}</text>
    </g>
  </g>
</template>

<style scoped>
text {
  background: var(--gray90);
  border-radius: 5px;
  padding: 0.5em;
}
</style>
