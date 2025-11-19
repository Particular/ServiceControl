<script setup lang="ts">
const props = defineProps<{
  supported: boolean;
}>();
</script>

<template>
  <template v-if="!props.supported">
    <slot name="unsupported" />
  </template>
  <template v-else>
    <slot />
  </template>
</template>
