<script setup lang="ts">
import { useFormatTime } from "@/composables/formatter";
import { Handler } from "@/resources/SequenceDiagram/Handler";

defineProps<{ handler: Handler }>();

function formatTime(milliseconds: number) {
  const time = useFormatTime(milliseconds);
  return `${time.value} ${time.unit}`;
}
</script>

<template>
  <div v-if="handler.id === 'First'">Start of Conversation</div>
  <div v-else class="handler-tooltip">
    <div class="title">Processing of Message</div>
    <div class="details">
      <label>Processing Time:</label>
      <span>{{ formatTime(handler.processingTime ?? 0) }}</span>
      <label>Processing Of:</label>
      <span>{{ handler.name }}</span>
      <label v-if="handler.partOfSaga">Sagas Invoked:</label>
      <span v-if="handler.partOfSaga">{{ handler.partOfSaga }}</span>
    </div>
  </div>
</template>

<style>
.handler-tooltip {
  display: flex;
  flex-direction: column;
}

.handler-tooltip .title {
  font-weight: bold;
}

.handler-tooltip .details {
  display: grid;
  grid-template-columns: auto auto;
  column-gap: 0.5em;
}

.handler-tooltip label {
  grid-column: 1;
  justify-self: end;
  font-weight: bold;
  color: #b3b3b3;
}

.handler-tooltip span {
  word-break: break-all;
}
</style>
