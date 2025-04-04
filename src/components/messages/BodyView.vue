<script setup lang="ts">
import { ExtendedFailedMessage } from "@/resources/FailedMessage";
import { computed } from "vue";
import CodeEditor from "@/components/CodeEditor.vue";
import parseContentType from "@/composables/contentTypeParser";
const props = defineProps<{
  message: ExtendedFailedMessage;
}>();

const contentType = computed(() => parseContentType(props.message.contentType));
</script>

<template>
  <div v-if="props.message.messageBodyNotFound" class="alert alert-info">Could not find message body. This could be because the message URL is invalid or the corresponding message was processed and is no longer tracked by ServiceControl.</div>
  <div v-else-if="props.message.bodyUnavailable" class="alert alert-info">Message body unavailable.</div>
  <CodeEditor v-else-if="contentType.isSupported" :model-value="props.message.messageBody" :language="contentType.language" :read-only="true" :show-gutter="true"></CodeEditor>
  <div v-else class="alert alert-warning">Message body cannot be displayed because content type "{{ props.message.contentType }}" is not supported.</div>
</template>

<style scoped></style>
