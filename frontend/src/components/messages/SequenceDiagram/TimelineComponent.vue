<script setup lang="ts">
import { useSequenceDiagramStore } from "@/stores/SequenceDiagramStore";
import { storeToRefs } from "pinia";

const store = useSequenceDiagramStore();
const { endpointCentrePoints, maxHeight } = storeToRefs(store);
</script>

<template>
  <g v-for="timeline in endpointCentrePoints" :key="timeline.name">
    <path v-if="timeline.centre" :d="`M${timeline.centre} ${timeline.top} v0 ${maxHeight - timeline.top}`" stroke="var(--gray90)" stroke-width="2" />
  </g>
</template>
