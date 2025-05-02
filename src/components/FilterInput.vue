<script setup lang="ts">
import { computed } from "vue";
import debounce from "lodash/debounce";

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

const debounceUpdateModel = debounce((value: string) => {
  model.value = value;
}, 600);
</script>

<template>
  <div role="search" aria-label="filter" class="filter-input">
    <input type="search" @focus="() => emit('focus')" @blur="() => emit('blur')" :placeholder="props.placeholder" :aria-label="props.ariaLabel" class="form-control filter-input" v-model="localInput" />
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
