<script setup lang="ts">
import { WarningLevel } from "@/components/WarningLevel";
import FAIcon from "@/components/FAIcon.vue";
import { faExclamationTriangle } from "@fortawesome/free-solid-svg-icons";

const props = withDefaults(
  defineProps<{
    type?: WarningLevel;
  }>(),
  { type: WarningLevel.None }
);
</script>

<template>
  <template v-if="props.type !== WarningLevel.None">
    <FAIcon :icon="faExclamationTriangle" :class="props.type === WarningLevel.Danger ? 'danger' : 'warning'" />
  </template>
</template>

<style scoped>
.danger,
.warning {
  margin: 0 2px;
}

.danger {
  color: #ce4844 !important;
  background: linear-gradient(white, white) center/20% 72% no-repeat;
}

.warning {
  color: var(--reduced-emphasis);
}
</style>
