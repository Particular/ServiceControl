<script setup lang="ts">
import { ExtendedFailedMessage } from "@/resources/FailedMessage";
import VCodeBlock from "@wdns/vue-code-block";

const props = defineProps<{
  message: ExtendedFailedMessage;
}>();
</script>

<template>
  <VCodeBlock :code="props.message.exception.stack_trace" lang="csharp"></VCodeBlock>
</template>
