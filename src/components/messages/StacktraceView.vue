<script setup lang="ts">
import CodeEditor from "@/components/CodeEditor.vue";
import { useMessageViewStore } from "@/stores/MessageViewStore.ts";
import LoadingSpinner from "@/components/LoadingSpinner.vue";

const store = useMessageViewStore();
</script>

<template>
  <div v-if="store.state.failed_to_load" class="alert alert-info">Stacktrace not available.</div>
  <LoadingSpinner v-else-if="store.state.loading" />
  <CodeEditor v-else :model-value="store.state.data.failure_metadata.exception?.stack_trace!" language="powershell" :read-only="true" :show-gutter="false"></CodeEditor>
</template>
