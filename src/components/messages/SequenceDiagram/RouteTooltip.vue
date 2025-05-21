<script setup lang="ts">
import { RoutedMessage, RoutedMessageType } from "@/resources/SequenceDiagram/RoutedMessage";

defineProps<{ routedMessage: RoutedMessage }>();
</script>

<template>
  <div class="route-tooltip">
    <div class="title">{{ RoutedMessageType[routedMessage.type] }}</div>
    <div class="details">
      <label>Message Type:</label>
      <span>{{ routedMessage.name }}</span>
      <label>Message Id:</label>
      <span>{{ routedMessage.messageId }}</span>
      <label>Sent From:</label>
      <span>{{ `${routedMessage.sending.name}@${routedMessage.sending.host}` }}</span>
      <label>Sent To:</label>
      <span>{{ `${routedMessage.receiving.name}@${routedMessage.receiving.host}` }}</span>
    </div>
  </div>
</template>

<style>
.route-tooltip {
  display: flex;
  flex-direction: column;
}

.route-tooltip .title {
  font-weight: bold;
}

.route-tooltip .details {
  display: grid;
  grid-template-columns: auto auto;
  column-gap: 0.5em;
}

.route-tooltip label {
  grid-column: 1;
  justify-self: end;
  font-weight: bold;
  color: #b3b3b3;
}

.route-tooltip span {
  word-break: break-all;
}
</style>
