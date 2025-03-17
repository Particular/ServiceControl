<script setup lang="ts">
import { ExtendedFailedMessage } from "@/resources/FailedMessage";
import CodeEditor from "@/components/CodeEditor.vue";

const props = defineProps<{
  message: ExtendedFailedMessage;
}>();
</script>

<template>
  <CodeEditor :model-value="props.message.exception.stack_trace" language="powershell" :read-only="true" :show-gutter="false"></CodeEditor>
</template>
