<script setup lang="ts">
import { defineProps } from "vue";

const props = withDefaults(
  defineProps<{
    interactive?: boolean;
  }>(),
  {
    interactive: false,
  }
);
</script>

<template>
  <tippy max-width="400px" :interactive="props.interactive">
    <i class="fa fa-sm fa-info-circle text-primary ps-1" />
    <template #content>
      <slot />
    </template>
  </tippy>
</template>
