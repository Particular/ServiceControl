<script setup lang="ts">
import FilterInput from "@/components/FilterInput.vue";
import { onMounted, ref, useTemplateRef, watch } from "vue";

const selected = defineModel<string>({ required: true });
const props = withDefaults(
  defineProps<{
    items: string[];
    instructions?: string;
    itemName: string;
    defaultEmptyText?: string;
    canClear?: boolean;
    showClear: boolean;
    showFilter: boolean;
  }>(),
  { canClear: true }
);
const filter = ref("");
const filteredItems = ref(props.items);

watch([filter, () => props.items], () => {
  if (filter.value !== "" && filter.value != null) {
    filteredItems.value = props.items.filter((item) => item.toLowerCase().includes(filter.value.toLowerCase()));
  } else {
    filteredItems.value = props.items;
  }
});

function setFilter(item: string, isSelected: boolean) {
  selected.value = isSelected && props.canClear ? "" : item;
}
const bootstrapDropDown = useTemplateRef<HTMLElement | null>("bootstrapDropDown");
const filterInput = useTemplateRef<{ focus: () => void } | null>("filterInput");
onMounted(() => {
  bootstrapDropDown.value?.addEventListener("shown.bs.dropdown", () => {
    filterInput.value?.focus();
  });
});
</script>

<template>
  <div ref="bootstrapDropDown" class="dropdown">
    <button type="button" aria-label="open dropdown menu" class="btn btn-dropdown dropdown-toggle sp-btn-menu" data-bs-toggle="dropdown" aria-haspopup="true" aria-expanded="false">
      <span class="wrap-text">{{ selected || defaultEmptyText }}</span>
    </button>
    <div class="dropdown-menu wrapper">
      <div class="instructions">{{ instructions }}</div>
      <div v-if="showFilter" class="filter-input">
        <FilterInput ref="filterInput" v-model="filter" :placeholder="`Filter ${itemName}s`" />
      </div>
      <div class="items-container">
        <div class="item-container" v-if="showClear && selected" @click.prevent="() => setFilter('', true)">
          <i class="fa fa-times" />
          <span class="clear"> Clear selected {{ itemName }}</span>
        </div>
        <div class="item-container" v-for="item in filteredItems" :key="item" @click.prevent="() => setFilter(item, item === selected)">
          <i v-if="item === selected" class="fa fa-check" />
          <span class="item" :class="{ selected: item === selected }">{{ item }}</span>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.wrap-text {
  max-width: 18em;
  word-wrap: break-word;
}
.wrapper {
  padding: 0.5rem;
  min-width: 12.5rem;
}
.instructions {
  font-weight: bold;
  margin-bottom: 0.5rem;
}
.items-container {
  max-height: 18rem;
  max-width: 25rem;
  overflow-y: auto;
}
.item {
  font-size: 0.875rem;
  padding-left: 1.25rem;
  font-weight: 400;
  color: #262626;
  text-decoration: none;
  width: 100%;
  display: flex;
  overflow-wrap: anywhere;
}
.item-container {
  padding: 0.25rem 0;
  display: flex;
  place-items: center;
  cursor: pointer;
  max-width: 100%;
  width: max-content;
}

.item-container:hover {
  background-color: #f5f5f5;
}

.filter-input {
  margin-bottom: 0.5rem;
}

.clear {
  color: #262626;
  font-weight: 400;
  margin-left: 0.375rem;
}

.selected {
  padding-left: 0.375rem;
}

.dropdown .btn {
  padding-left: 0.5rem;
}
</style>
