<script setup lang="ts">
import { Endpoint } from "@/resources/SequenceDiagram/Endpoint";

defineProps<{ endpoint: Endpoint }>();
</script>

<template>
  <div class="endpoint-tooltip">
    <label>Name:</label>
    <span>{{ endpoint.name }}</span>
    <label>NSB Version:</label>
    <span>{{ endpoint.version }}</span>
    <label>Host:</label>
    <span>{{ endpoint.host }}</span>
  </div>
</template>

<style>
.endpoint-tooltip {
  display: grid;
  grid-template-columns: auto auto;
  column-gap: 0.5em;
}

.endpoint-tooltip label {
  grid-column: 1;
  justify-self: end;
  font-weight: bold;
  color: #b3b3b3;
}

.endpoint-tooltip span {
  word-break: break-all;
}
</style>
