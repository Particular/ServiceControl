<script setup lang="ts">
import FAIcon from "@/components/FAIcon.vue";
import { IconDefinition } from "@fortawesome/free-solid-svg-icons";

interface Props {
  icon?: IconDefinition;
}

defineProps<Props>();
</script>

<template>
  <span class="metadata">
    <FAIcon v-if="icon" :icon="icon" class="icon" />
    <slot />
  </span>
</template>

<style scoped>
.metadata {
  display: inline-flex;
  align-items: center;
  gap: 0.25rem;
}

.icon {
  margin-right: 0.25rem;
  color: var(--reduced-emphasis);
}
</style>
