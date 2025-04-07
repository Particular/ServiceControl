<script setup lang="ts">
import { ref, watch } from "vue";
import debounce from "lodash/debounce";

const model = defineModel<string>({ required: true });
const props = withDefaults(defineProps<{ placeholder?: string; ariaLabel?: string }>(), { placeholder: "Filter by name...", ariaLabel: "filter by name" });
const localInput = ref<string>(model.value);

const debounceUpdateModel = debounce((value: string) => {
  model.value = value;
}, 600);

watch(localInput, (newValue) => {
  debounceUpdateModel(newValue);
});
</script>

<template>
  <div role="search" aria-label="filter" class="filter-input">
    <input type="search" :placeholder="props.placeholder" :aria-label="props.ariaLabel" class="form-control-static filter-input" v-model="localInput" />
  </div>
</template>

<style scoped>
.filter-input input {
  display: inline-block;
  width: 100%;
  padding-right: 10px;
  padding-left: 30px;
  border: 1px solid #aaa;
  border-radius: 4px;
  height: 100%;
}

div.filter-input {
  position: relative;
  width: 280px;
  height: 36px;
}

.filter-input:before {
  font-family: "FontAwesome";
  width: 1.43em;
  content: "\f0b0";
  color: #919e9e;
  position: absolute;
  top: calc(50% - 0.7em);
  left: 0.75em;
}
</style>
