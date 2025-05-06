<script setup lang="ts">
import Endpoints from "./SequenceDiagram/EndpointsComponent.vue";
import Timeline from "./SequenceDiagram/TimelineComponent.vue";
import Handlers from "./SequenceDiagram/HandlersComponent.vue";
import Routes from "./SequenceDiagram/RoutesComponent.vue";
import { useSequenceDiagramStore } from "@/stores/SequenceDiagramStore";
import { storeToRefs } from "pinia";
import useTooltips from "./SequenceDiagram/tooltipOverlay.ts";
import { onMounted, ref } from "vue";
import LoadingSpinner from "@/components/LoadingSpinner.vue";
const store = useSequenceDiagramStore();
const { maxWidth, maxHeight } = storeToRefs(store);
const endpointYOffset = ref(0);

useTooltips();

onMounted(() => store.refreshConversation());
</script>

<template>
  <div class="wrapper">
    <div class="toolbar">
      <a class="help-link" target="_blank" href="https://docs.particular.net/servicepulse/sequence-diagram"><i class="fa fa-info-circle" /> Sequence Diagram Help</a>
    </div>
    <LoadingSpinner v-if="store.isLoading" />
    <div class="outer" @scroll="(ev) => (endpointYOffset = (ev.target as Element).scrollTop)">
      <svg class="sequence-diagram" :style="{ width: `max(100%, ${isNaN(maxWidth) ? 0 : maxWidth}px)` }" :height="maxHeight + 20">
        <Timeline />
        <Handlers />
        <Routes />
        <Endpoints :yOffset="endpointYOffset" />
      </svg>
    </div>
  </div>
</template>

<style scoped>
.wrapper {
  margin-top: 5px;
  border-radius: 0.5rem;
  padding: 0.5rem;
  border: 1px solid #ccc;
  background: white;
  display: flex;
  flex-direction: column;
}
.toolbar {
  background-color: #f3f3f3;
  border: #8c8c8c 1px solid;
  border-radius: 3px;
  padding: 5px;
  margin-bottom: 0.5rem;
  display: flex;
  flex-direction: row;
  justify-content: end;
  min-height: 40px;
}
.outer {
  max-width: 100%;
  max-height: calc(100vh - 30em);
  overflow: auto;
}

.sequence-diagram {
  --error: red;
  --gray20: #333333;
  --gray30: #444444;
  --gray40: #666666;
  --gray60: #999999;
  --gray80: #cccccc;
  --gray90: #e6e6e6;
  --gray95: #b3b3b3;
  --highlight: #0b6eef;
  --highlight-background: #c5dee9;
  background: white;
}

.help-link {
  display: flex;
  align-items: center;
  justify-content: end;
  gap: 0.15rem;
}
</style>
