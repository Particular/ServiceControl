<script setup lang="ts">
import { computed, useTemplateRef } from "vue";
import debounce from "lodash/debounce";
import FAIcon from "./FAIcon.vue";
import { faFilter } from "@fortawesome/free-solid-svg-icons";

const model = defineModel<string>({ required: true });
const emit = defineEmits<{ focus: []; blur: [] }>();
const props = withDefaults(defineProps<{ placeholder?: string; ariaLabel?: string }>(), { placeholder: "Filter by name...", ariaLabel: "Filter by name" });
const localInput = computed({
  get() {
    return model.value;
  },
  set(newValue) {
    debounceUpdateModel(newValue);
  },
});
const textField = useTemplateRef<HTMLInputElement>("textField");
const debounceUpdateModel = debounce((value: string) => {
  model.value = value;
}, 600);

defineExpose({ focus });

function focus() {
  textField.value?.focus();
}
</script>

<template>
  <div role="search" aria-label="filter" class="filter-input">
    <FAIcon :icon="faFilter" class="icon" />
    <input ref="textField" type="search" @focus="() => emit('focus')" @blur="() => emit('blur')" :placeholder="props.placeholder" :aria-label="props.ariaLabel" class="form-control filter-input" v-model="localInput" />
  </div>
</template>

<style scoped>
.filter-input input {
  display: inline-block;
  width: 100%;
  padding-right: 0.625rem;
  padding-left: 2em;
  border: 1px solid #aaa;
  border-radius: 4px;
  height: 100%;
}

div.filter-input {
  position: relative;
  height: 2.6em;
}

.filter-input .icon {
  position: absolute;
  left: 0.75em;
  top: calc(50% - 0.5em);
  color: var(--reduced-emphasis);
}
</style>
