<script setup lang="ts">
import { ref, watch } from "vue";
import ListFilterSelector from "@/components/audit/ListFilterSelector.vue";

const props = defineProps<{ isLoading: boolean }>();
const model = defineModel<number | null>({ required: true });
const emit = defineEmits<{ (e: "manualRefresh"): Promise<void> }>();
const autoRefreshOptionsText = ["Off", "Every 5 seconds", "Every 15 seconds", "Every 30 seconds"];
let selectValue = "Off";

if (model.value === 5000) {
  selectValue = "Every 5 seconds";
}
if (model.value === 15000) {
  selectValue = "Every 15 seconds";
}
if (model.value === 30000) {
  selectValue = "Every 30 seconds";
}

const selectedRefresh = ref<string>(selectValue);

watch(selectedRefresh, (newValue) => {
  if (newValue === autoRefreshOptionsText[0]) {
    model.value = null;
  }
  if (newValue === autoRefreshOptionsText[1]) {
    model.value = 5000;
  }
  if (newValue === autoRefreshOptionsText[2]) {
    model.value = 15000;
  }
  if (newValue === autoRefreshOptionsText[3]) {
    model.value = 30000;
  }
});

async function refresh() {
  await emit("manualRefresh");
}
</script>

<template>
  <div class="refresh-config">
    <button class="btn btn-sm" title="refresh" @click="refresh"><i class="fa fa-refresh" :class="{ spinning: props.isLoading }" /> Refresh List</button>
    <div class="filter">
      <div class="filter-label">Auto-Refresh:</div>
      <div class="filter-component">
        <ListFilterSelector :items="autoRefreshOptionsText" v-model="selectedRefresh" item-name="result" :can-clear="false" :show-clear="false" :show-filter="false" />
      </div>
    </div>
  </div>
</template>

<style scoped>
.refresh-config {
  display: flex;
  align-items: center;
  gap: 1em;
  margin-bottom: 0.5em;
}

.filter {
  display: flex;
  align-items: center;
}

.filter-label {
  font-weight: bold;
}

@keyframes spin {
  from {
    transform: rotate(0deg);
  }
  to {
    transform: rotate(360deg);
  }
}

.fa-refresh {
  display: inline-block;
}

/* You can add this class dynamically when needed */
.fa-refresh.spinning {
  animation: spin 1s linear infinite;
}
</style>
