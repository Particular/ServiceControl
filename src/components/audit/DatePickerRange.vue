<script setup lang="ts">
import type { DateRange } from "@/types/date";
import VueDatePicker from "@vuepic/vue-datepicker";
import "@vuepic/vue-datepicker/dist/main.css";
import { ref, useTemplateRef, watch } from "vue";
import { useDateFormatter } from "@/composables/dateFormatter";

const { formatDateRange, isValidDateRange } = useDateFormatter();

const model = defineModel<DateRange>({ required: true });
const internalModel = ref<DateRange>([]);
const displayDataRange = ref<string>("No dates");
const datePicker = useTemplateRef<typeof VueDatePicker>("datePicker");

watch(internalModel, () => {
  const updatedRange = internalModel.value as DateRange;
  if (isValidDateRange(updatedRange)) {
    model.value = updatedRange;
    displayDataRange.value = formatDateRange(updatedRange);
  } else internalModel.value = model.value;
});

watch(
  model,
  () => {
    internalModel.value = model.value;
  },
  { immediate: true }
);

function clearCurrentDate() {
  internalModel.value = [];
  datePicker.value?.closeMenu();
}
</script>

<template>
  <VueDatePicker
    ref="datePicker"
    class="dropdown"
    v-model="internalModel"
    :format="(dates: Date[]) => formatDateRange(dates as DateRange)"
    :range="{ partialRange: false }"
    :enable-seconds="true"
    :action-row="{ showNow: false, showCancel: false, showSelect: true }"
  >
    <template #trigger>
      <button type="button" class="btn btn-dropdown dropdown-toggle sp-btn-menu">
        {{ displayDataRange }}
      </button>
    </template>
    <template #action-extra>
      <button v-if="internalModel.length === 2" class="dp__action_button dp__action_cancel" @click="clearCurrentDate()">Clear Range</button>
    </template>
  </VueDatePicker>
</template>

<style>
.dropdown .btn {
  padding-left: 0.5rem;
}
</style>
