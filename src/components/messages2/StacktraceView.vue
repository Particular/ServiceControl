<script setup lang="ts">
import { useMessageStore } from "@/stores/MessageStore";
import LoadingSpinner from "@/components/LoadingSpinner.vue";
import { storeToRefs } from "pinia";
import StacktraceFormatter from "@/components/messages2/StacktraceFormatter.vue";
import CopyToClipboard from "@/components/CopyToClipboard.vue";

const { state } = storeToRefs(useMessageStore());
</script>

<template>
  <div v-if="state.failed_to_load" class="alert alert-info">Stacktrace not available.</div>
  <LoadingSpinner v-else-if="state.loading" />
  <div v-else class="wrapper">
    <div class="toolbar">
      <CopyToClipboard class="clipboard" :value="state.data.failure_metadata.exception?.stack_trace!" />
    </div>
    <StacktraceFormatter :stack-trace="state.data.failure_metadata.exception?.stack_trace!" />
  </div>
</template>

<style scoped>
.toolbar {
  background-color: #f3f3f3;
  border: #8c8c8c 1px solid;
  border-radius: 3px;
  padding: 5px;
  margin-bottom: 0.5rem;
  display: flex;
  flex-direction: row;
  justify-content: flex-end;
  align-items: center;
}

.wrapper {
  margin-top: 5px;
  margin-bottom: 15px;
  border-radius: 0.5rem;
  padding: 0.5rem;
  border: 1px solid #ccc;
  background: white;
}
</style>
