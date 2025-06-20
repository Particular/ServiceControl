<script setup lang="ts">
import routeLinks from "@/router/routeLinks";
import Message, { MessageStatus } from "@/resources/Message";
import { defineProps } from "vue";
import { formatDotNetTimespan } from "@/composables/formatUtils";
import { useRoute, useRouter } from "vue-router";
import MessageStatusIcon from "@/components/audit/MessageStatusIcon.vue";

const route = useRoute();
const router = useRouter();

const props = defineProps<{
  message: Message;
}>();

function navigateToMessage(message: Message) {
  const query = router.currentRoute.value.query;

  router.push({
    path: message.status === MessageStatus.Successful || message.status === MessageStatus.ResolvedSuccessfully ? routeLinks.messages.successMessage.link(message.message_id, message.id) : routeLinks.messages.failedMessage.link(message.id),
    query: { ...query, ...{ back: route.path } },
  });
}
</script>

<template>
  <div class="item" @click="navigateToMessage(props.message)">
    <div class="status">
      <MessageStatusIcon :message="props.message" />
    </div>
    <div class="message-id">{{ props.message.message_id }}</div>
    <div class="message-type">{{ props.message.message_type }}</div>
    <div class="time-sent"><span class="label-name">Time Sent:</span>{{ new Date(props.message.time_sent).toLocaleString() }}</div>
    <div class="critical-time"><span class="label-name">Critical Time:</span>{{ formatDotNetTimespan(props.message.critical_time) }}</div>
    <div class="processing-time"><span class="label-name">Processing Time:</span>{{ formatDotNetTimespan(props.message.processing_time) }}</div>
    <div class="delivery-time"><span class="label-name">Delivery Time:</span>{{ formatDotNetTimespan(props.message.delivery_time) }}</div>
  </div>
</template>

<style scoped>
.item {
  padding: 0.3rem 0.2rem;
  border: 1px solid #ffffff;
  border-bottom: 1px solid #eee;
  display: grid;
  grid-template-columns: 1.8em 1fr 1fr 1fr 1fr;
  grid-template-rows: 1fr 1fr;
  gap: 0.375rem;
  grid-template-areas:
    "status message-type message-type message-type time-sent"
    "status message-id processing-time critical-time delivery-time";
}
.item:not(:first-child) {
  border-top-color: #eee;
}
.item:hover {
  border-color: #00a3c4;
  background-color: #edf6f7;
  cursor: pointer;
}
.label-name {
  margin-right: 0.25rem;
  color: #777f7f;
}
.status {
  grid-area: status;
}
.message-id {
  grid-area: message-id;
}
.time-sent {
  grid-area: time-sent;
}
.message-type {
  grid-area: message-type;
  font-weight: bold;
  overflow-wrap: break-word;
}
.processing-time {
  grid-area: processing-time;
}
.critical-time {
  grid-area: critical-time;
}
.delivery-time {
  grid-area: delivery-time;
}
</style>
