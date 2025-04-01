<script setup lang="ts">
import { computed, watch } from "vue";
import CodeEditor from "@/components/CodeEditor.vue";
import parseContentType from "@/composables/contentTypeParser";
import { useMessageViewStore } from "@/stores/MessageViewStore.ts";
import LoadingSpinner from "@/components/LoadingSpinner.vue";

const store = useMessageViewStore();
watch(
  () => store.state.data.body_url,
  async () => {
    await store.downloadBody();
  },
  { immediate: true }
);
const contentType = computed(() => parseContentType(store.body.data.content_type));
const body = computed(() => store.body.data.value);
</script>

<template>
  <div v-if="store.body.not_found" class="alert alert-info">Could not find message body. This could be because the message URL is invalid or the corresponding message was processed and is no longer tracked by ServiceControl.</div>
  <div v-else-if="store.body.failed_to_load" class="alert alert-info">Message body unavailable.</div>
  <LoadingSpinner v-else-if="store.body.loading" />
  <CodeEditor v-else-if="body !== undefined && contentType.isSupported" :model-value="body" :language="contentType.language" :read-only="true" :show-gutter="true"></CodeEditor>
  <div v-else class="alert alert-warning">Message body cannot be displayed because content type "{{ store.body.data.content_type }}" is not supported.</div>
</template>

<style scoped></style>
