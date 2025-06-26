<script setup lang="ts">
import { computed, useSlots } from "vue";
import type { SortInfo } from "./SortInfo";

const props = withDefaults(
  defineProps<{
    name: string;
    label: string;
    sortable?: boolean;
    sortBy?: string;
    sortState?: SortInfo;
    defaultAscending?: boolean;
    columnClass: string;
    interactiveHelp?: boolean;
  }>(),
  {
    sortable: false,
    defaultAscending: false,
    interactiveHelp: false,
  }
);

const slots = useSlots();
const sortByColumn = computed(() => props.sortBy || props.name);
const activeSortColumn = defineModel<SortInfo>({ required: true });
const isSortActive = computed(() => activeSortColumn.value?.property === sortByColumn.value);
const sortIcon = computed(() => (activeSortColumn.value.isAscending ? "sort-up" : "sort-down"));

function toggleSort() {
  activeSortColumn.value = { property: sortByColumn.value, isAscending: isSortActive.value ? !activeSortColumn.value.isAscending : props.defaultAscending };
}
</script>

<template>
  <div role="columnheader" :aria-label="props.name" :class="props.columnClass">
    <div class="box-header">
      <button v-if="props.sortable" @click="toggleSort" class="column-header-button" :aria-label="props.name">
        <span>
          {{ props.label }}
          <span class="table-header-unit"><slot name="unit"></slot></span>
          <span v-if="isSortActive">
            <i role="img" :class="sortIcon" :aria-label="sortIcon"></i>
          </span>
        </span>
        <tippy v-if="slots.help" max-width="400px" :interactive="props.interactiveHelp">
          <i class="fa fa-sm fa-info-circle text-primary ps-1" />
          <template #content>
            <slot name="help" />
          </template>
        </tippy>
      </button>
      <div v-else class="column-header">
        <span>
          {{ props.label }}
          <span class="table-header-unit"><slot name="unit"></slot></span>
        </span>
        <tippy v-if="slots.help" max-width="400px" :interactive="props.interactiveHelp">
          <i class="fa fa-sm fa-info-circle text-primary ps-1" />
          <template #content>
            <slot name="help" />
          </template>
        </tippy>
      </div>
    </div>
  </div>
</template>

<style scoped>
.column-header {
  background: none;
  border: none;
  padding: 0;
  cursor: default;
  max-width: 100%;
  display: flex;
  flex-wrap: wrap;
}
.column-header span,
.column-header-button span {
  text-transform: uppercase;
  display: inline-block;
  text-align: left;
}
.column-header-button {
  background: none;
  border: none;
  padding: 0;
  cursor: pointer;
  max-width: 100%;
  display: flex;
  flex-wrap: wrap;
  align-items: end;
}

.column-header-button:hover span {
  text-decoration: underline;
}

.column-header-button div {
  display: inline-block;
}

.sort-up,
.sort-down {
  background-position: center;
  background-repeat: no-repeat;
  width: 8px;
  height: 14px;
  padding: 0;
  margin-left: 10px;
}

.sort-up {
  background-image: url("@/assets/sort-up.svg");
}

.sort-down {
  background: url("@/assets/sort-down.svg");
}

.sort-up,
.sort-down {
  background-repeat: no-repeat;
  display: inline-block;
  vertical-align: middle;
}
</style>
