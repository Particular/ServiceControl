<script setup lang="ts">
import { defineProps } from "vue";
const props = defineProps<{
  columnLabel: string;
}>();
</script>

<template>
  <div role="columnheader" aria-label="actions" class="col-1">
    <span>{{ props.columnLabel }}</span>
    <slot />
  </div>
</template>

<style scoped>
.column-header-button span {
  display: flex;
  align-items: center;
}
</style>
