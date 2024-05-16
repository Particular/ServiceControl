<script setup lang="ts">
import { ConnectionSettingsTestResult } from "@/resources/ConnectionTestResults";

const props = defineProps<{
  result: ConnectionSettingsTestResult;
  title: string;
}>();
</script>

<template>
  <div class="title">
    <i v-if="result.connection_successful" style="color: green" class="fa fa-check"></i>
    <i v-else style="color: red" class="fa fa-times"></i>
    <strong> {{ props.title }} </strong>
  </div>
  <div class="instructions"><slot name="instructions"></slot></div>
  <pre>{{ props.result.diagnostics }}</pre>
</template>

<style scoped>
.title {
  margin-top: 10px;
}
.instructions {
  margin-bottom: 10px;
}
</style>
