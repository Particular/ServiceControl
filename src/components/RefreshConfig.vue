<script setup lang="ts">
import { ref } from "vue";
import OnOffSwitch from "./OnOffSwitch.vue";

const props = defineProps<{
  id: string;
  initialTimeout?: number;
  onManualRefresh: () => void;
}>();

const emit = defineEmits<{ change: [newValue: number | null]; manualRefresh: [] }>();

const autoRefresh = ref(props.initialTimeout != null);
const refreshTimeout = ref(props.initialTimeout ?? 5);

function toggleRefresh() {
  autoRefresh.value = !autoRefresh.value;
  updateTimeout();
}

function updateTimeout() {
  validateTimeout();
  emit("change", autoRefresh.value ? refreshTimeout.value * 1000 : null);
}

function validateTimeout() {
  refreshTimeout.value = Math.max(1, Math.min(600, refreshTimeout.value));
}
</script>

<template>
  <div class="refresh-config">
    <button class="fa" title="refresh" @click="() => emit('manualRefresh')">
      <i class="fa fa-lg fa-refresh" />
    </button>
    <span>|</span>
    <label>Auto-Refresh:</label>
    <div>
      <OnOffSwitch :id="id" @toggle="toggleRefresh" :value="autoRefresh" />
    </div>
    <input type="number" v-model="refreshTimeout" min="1" max="600" v-on:change="updateTimeout" />
    <span class="unit">s</span>
  </div>
</template>

<style scoped>
.refresh-config {
  display: flex;
  align-items: center;
  gap: 0.5em;
}

.refresh-config .unit {
  margin-left: -0.45em;
}

.refresh-config label {
  margin: 0;
}

.refresh-config input {
  width: 3.5em;
}

.refresh-config button {
  background: none;
  border: none;
  width: 2em;
}

.refresh-config button .fa {
  transition: all 0.15s ease-in-out;
  transition: rotate 0.05s ease-in-out;
  transform-origin: center;
}

.refresh-config button:hover .fa {
  color: #00a3c4;
  transform: scale(1.1);
}

.refresh-config button:active .fa {
  transform: rotate(25deg);
  text-shadow: #929e9e 0.25px 0.25px;
}
</style>
