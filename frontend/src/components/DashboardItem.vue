<script setup lang="ts">
import { IconDefinition } from "@fortawesome/free-solid-svg-icons";
import FAIcon from "@/components/FAIcon.vue";

defineProps<{
  counter: number;
  url: string;
  icon: IconDefinition;
}>();
</script>

<template>
  <RouterLink aria-label="Dashboard Item" class="summary-item" :class="{ 'summary-danger': counter > 0, 'summary-info': counter === 0 || !counter }" :to="url">
    <FAIcon :icon="icon" size="3x" />
    <span v-if="counter > 0" aria-label="Alert Count" class="badge badge-important">{{ counter }}</span>
    <h4>
      <slot></slot>
    </h4>
  </RouterLink>
</template>

<style scoped>
.summary-item {
  background: #fff none repeat scroll 0 0;
  border: 1px solid #fff;
  border-radius: 4px;
  color: #777f7f;
  display: block;
  padding: 25px 10px;
  position: relative;
  text-align: center;
}

.summary-item .badge,
.summary-item .label {
  font-size: 18px;
  margin-left: 12px;
  position: absolute;
  top: 2px;
}

.summary-info,
.summary-info > .fa,
a.summary-info:hover {
  color: #777f7f;
}

.summary-danger,
.summary-danger > .fa,
a.summary-danger:hover {
  color: #ce4844;
  font-weight: bold;
}

.summary-item:hover {
  background-color: #edf6f7;
  border-color: var(--sp-blue) !important;
  cursor: pointer;
}
</style>
