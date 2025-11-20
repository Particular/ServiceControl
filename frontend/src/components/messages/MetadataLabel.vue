<script setup lang="ts">
const props = defineProps<{ tooltip: string; type: "info" | "warning" | "important"; text: string }>();
</script>
<template>
  <span v-tippy="props.tooltip" class="label sidebar-label metadata-label" :class="`label-${props.type}`">{{ props.text }}</span>
</template>
