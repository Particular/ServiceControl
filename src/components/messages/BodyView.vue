<script setup lang="ts">
import { ExtendedFailedMessage } from "@/resources/FailedMessage";

const props = defineProps<{
  message: ExtendedFailedMessage;
}>();
</script>

<template>
  <div v-if="props.message.messageBodyNotFound" class="alert alert-info">Could not find message body. This could be because the message URL is invalid or the corresponding message was processed and is no longer tracked by ServiceControl.</div>
  <div v-else-if="props.message.bodyUnavailable" class="alert alert-info">Message body unavailable.</div>
  <pre v-else>{{ props.message.messageBody }}</pre>
</template>

<style scoped></style>
