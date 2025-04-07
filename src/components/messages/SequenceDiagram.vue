<script setup lang="ts">
import Endpoints from "./SequenceDiagram/EndpointsComponent.vue";
import Timeline from "./SequenceDiagram/TimelineComponent.vue";
import Handlers from "./SequenceDiagram/HandlersComponent.vue";
import Routes from "./SequenceDiagram/RoutesComponent.vue";
import { useSequenceDiagramStore } from "@/stores/SequenceDiagramStore";
import { storeToRefs } from "pinia";

const store = useSequenceDiagramStore();
store.setConversationId("39907d51-12e5-4202-82c3-b2b30077ebd4");

const { maxWidth, maxHeight } = storeToRefs(store);
</script>

<template>
  <div class="outer">
    <svg class="sequence-diagram" :width="`max(100%, ${isNaN(maxWidth) ? 0 : maxWidth}px)`" :height="maxHeight + 20">
      <Endpoints />
      <Timeline />
      <Handlers />
      <Routes />
    </svg>
  </div>
</template>

<style scoped>
.outer {
  max-width: 100%;
  max-height: calc(100vh - 27em);
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
</style>
