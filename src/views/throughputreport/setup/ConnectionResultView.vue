<script setup lang="ts">
import { ConnectionSettingsTestResult } from "@/resources/ConnectionTestResults";
import FAIcon from "@/components/FAIcon.vue";
import { faCheck, faTimes } from "@fortawesome/free-solid-svg-icons";

const props = defineProps<{
  result: ConnectionSettingsTestResult;
  title: string;
}>();
</script>

<template>
  <div class="title">
    <FAIcon v-if="result.connection_successful" :icon="faCheck" class="text-success" />
    <FAIcon v-else :icon="faTimes" class="text-danger" />
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
