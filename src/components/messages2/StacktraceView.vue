<script setup lang="ts">
import CodeEditor from "@/components/CodeEditor.vue";
import { useMessageViewStore } from "@/stores/MessageViewStore";
import LoadingSpinner from "@/components/LoadingSpinner.vue";
import { storeToRefs } from "pinia";

const { state } = storeToRefs(useMessageViewStore());
</script>

<template>
  <div v-if="state.failed_to_load" class="alert alert-info">Stacktrace not available.</div>
  <LoadingSpinner v-else-if="state.loading" />
  <CodeEditor v-else :model-value="state.data.failure_metadata.exception?.stack_trace!" language="powershell" :read-only="true" :show-gutter="false"></CodeEditor>
</template>
