<script setup lang="ts">
import { ConnectionSettingsTestResult } from "@/resources/ConnectionTestResults";

const props = defineProps<{
  result: ConnectionSettingsTestResult;
  title: string;
}>();
</script>

<template>
  <h5>
    <i v-if="result.connection_successful" style="color: green" class="fa fa-check"></i>
    <i v-else style="color: red" class="fa fa-times"></i>
    {{ props.title }}
  </h5>
  <pre>{{ props.result.diagnostics }}</pre>
</template>

<style scoped></style>
