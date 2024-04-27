<script setup lang="ts">
import { computed } from "vue";
import type { SortInfo } from "./SortInfo";

const props = withDefaults(
  defineProps<{
    sortBy: string;
    defaultAscending?: boolean;
  }>(),
  { defaultAscending: false }
);

const activeColumn = defineModel<SortInfo>({ required: true });

const isActive = computed(() => activeColumn.value?.property === props.sortBy);
const sortIcon = computed(() => (activeColumn.value.isAscending ? "sort-up" : "sort-down"));

function toggleSort() {
  activeColumn.value = { property: props.sortBy, isAscending: isActive.value ? !activeColumn.value.isAscending : props.defaultAscending };
}
</script>
<template>
  <div class="box-header">
    <button v-if="props.sortBy" @click="toggleSort" class="column-header-button" :aria-label="props.sortBy">
      <span>
        <slot></slot>
        <span class="table-header-unit"><slot name="unit"></slot></span>
        <span>
          <i role="image" v-if="isActive" :class="sortIcon" :aria-label="sortIcon"></i>
        </span>
      </span>
    </button>
    <div v-else class="column-header">
      <span>
        <slot></slot>
        <span class="table-header-unit"><slot name="unit"></slot></span>
      </span>
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
.column-header-button {
  background: none;
  border: none;
  padding: 0;
  cursor: pointer;
  max-width: 100%;
  display: flex;
  flex-wrap: wrap;
}

.column-header-button span {
  text-transform: uppercase;
  display: inline-block;
  text-align: left;
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
