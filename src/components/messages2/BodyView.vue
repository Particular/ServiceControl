<script setup lang="ts">
import { computed, watch } from "vue";
import CodeEditor from "@/components/CodeEditor.vue";
import parseContentType from "@/composables/contentTypeParser";
import { useMessageStore } from "@/stores/MessageStore";
import LoadingSpinner from "@/components/LoadingSpinner.vue";
import { storeToRefs } from "pinia";

const store = useMessageStore();
const { body: bodyState, state } = storeToRefs(store);

watch(
  () => state.value.data.body_url,
  async () => {
    await store.downloadBody();
  },
  { immediate: true }
);
const contentType = computed(() => parseContentType(bodyState.value.data.content_type));
const body = computed(() => bodyState.value.data.value);
</script>

<template>
  <div v-if="bodyState.not_found" class="alert alert-info">Could not find the message body. This could be because the message URL is invalid or the corresponding message was processed and is no longer tracked by ServiceControl.</div>
  <div v-else-if="bodyState.failed_to_load" class="alert alert-info">Message body unavailable.</div>
  <LoadingSpinner v-else-if="bodyState.loading" />
  <CodeEditor v-else-if="body !== undefined && contentType.isSupported" :model-value="body" :language="contentType.language" :read-only="true" :show-gutter="true"></CodeEditor>
  <div v-else class="alert alert-warning">Message body cannot be displayed because content type "{{ bodyState.data.content_type }}" is not supported.</div>
</template>

<style scoped></style>
