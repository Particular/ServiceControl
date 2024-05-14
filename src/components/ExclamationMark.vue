<script setup lang="ts">
import { WarningLevel } from "@/components/WarningLevel";

const props = withDefaults(
  defineProps<{
    type?: WarningLevel;
  }>(),
  { type: WarningLevel.None }
);
</script>

<template>
  <template v-if="props.type !== WarningLevel.None">
    <span class="fa fa-exclamation-triangle" :class="props.type === WarningLevel.Danger ? 'danger' : 'warning'"></span>
  </template>
</template>

<style scoped>
.danger {
  color: #ce4844 !important;
  font-weight: normal !important;
}
</style>
