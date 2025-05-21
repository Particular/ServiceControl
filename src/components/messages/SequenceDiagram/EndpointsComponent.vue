<script setup lang="ts">
import { Endpoint } from "@/resources/SequenceDiagram/Endpoint";
import { Endpoint_Width, EndpointCentrePoint, useSequenceDiagramStore } from "@/stores/SequenceDiagramStore";
import { storeToRefs } from "pinia";
import { computed, ref, watch } from "vue";

interface EndpointWithLocation extends Endpoint {
  width: number;
  textWidth: number;
  x: number;
  height: number;
}

const Endpoint_Gap = 30;
const Endpoint_Image_Width = 20;

defineProps<{
  yOffset: number;
}>();

const store = useSequenceDiagramStore();
const { startX, endpoints } = storeToRefs(store);

const epTextRefs = ref<Element[]>([]);
const endpointItems = computed(() =>
  endpoints.value.map((x, index) => {
    const endpoint = x as EndpointWithLocation;
    const el = epTextRefs.value[index];
    if (el) {
      const bounds = el.getBoundingClientRect();
      const previousEndpoint = index > 0 ? endpointItems.value[index - 1] : undefined;
      endpoint.width = Math.max(Endpoint_Width, bounds.width);
      endpoint.textWidth = bounds.width;
      endpoint.x = (previousEndpoint?.x ?? startX.value) + (previousEndpoint?.width ?? 0) + Endpoint_Gap;
      endpoint.height = bounds.height;
      endpoint.uiRef = el;
    }
    return endpoint;
  })
);

watch(endpointItems, () => {
  store.setEndpointCentrePoints(endpointItems.value.map((endpoint) => ({ name: endpoint.name, centre: endpoint.x ?? 0, top: (endpoint.height ?? 0) + 15 }) as EndpointCentrePoint));
  const lastEndpoint = endpointItems.value[endpointItems.value.length - 1];
  store.setMaxWidth((lastEndpoint.x ?? 0) + lastEndpoint.width);
});

watch(startX, () => {
  epTextRefs.value = [];
});

function setEndpointTextRef(el: Element, index: number) {
  if (el) epTextRefs.value[index] = el;
}
</script>

<template>
  <!-- occlusion for top of diagram so that elements don't show above the endpoint names when scrolling -->
  <rect width="100%" height="15" :transform="`translate(0,${yOffset})`" fill="white" />
  <g v-for="(endpoint, i) in endpointItems" :key="endpoint.name" :transform="`translate(0,${yOffset + 5})`" style="outline: none">
    <g :transform="`translate(${(endpoint.x ?? Endpoint_Width / 2) - ((endpoint.textWidth ?? 0) + Endpoint_Image_Width) / 2}, 0)`">
      <foreignObject :x="Endpoint_Image_Width" y="10" :width="Endpoint_Width" height="100%" style="pointer-events: none">
        <div class="endpoint-surround" :ref="(el) => setEndpointTextRef(el as Element, i)">
          <i class="endpoint-icon" />
          <div class="endpoint-name">{{ endpoint.name }}</div>
        </div>
      </foreignObject>
    </g>
  </g>
</template>

<style scoped>
.endpoint-surround {
  width: 100%;
  display: flex;
  background: var(--gray90);
  border-radius: 5px;
  padding: 0.5em;
  align-items: center;
  justify-content: center;
  gap: 0.5em;
  pointer-events: all;
}

.endpoint-icon {
  flex-shrink: 0;
  background-image: url("@/assets/endpoint.svg");
  background-position: center;
  background-repeat: no-repeat;
  height: 18px;
  width: 18px;
}

.endpoint-name {
  overflow-wrap: anywhere;
}
</style>
