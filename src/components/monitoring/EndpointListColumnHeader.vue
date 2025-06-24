<script setup lang="ts">
import SortableColumn from "../SortableColumn.vue";
import { SortInfo } from "../SortInfo";

const props = withDefaults(
  defineProps<{
    columnName: string;
    columnSort: string;
    displayName: string;
    displayUnit?: string;
    toolTip?: string;
    isFirstCol?: boolean;
  }>(),
  { isFirstCol: false }
);

const activeColumn = defineModel<SortInfo>({ required: true });
const defaultAscending = props.isFirstCol ? true : undefined;
</script>

<template>
  <div role="columnheader" :aria-label="columnName" :class="isFirstCol ? 'table-first-col' : 'table-col'">
    <SortableColumn :sort-by="columnSort" v-model="activeColumn" :default-ascending="defaultAscending" v-tippy="toolTip">
      {{ displayName }}<template v-if="displayUnit" #unit>&nbsp;{{ displayUnit }}</template>
    </SortableColumn>
  </div>
</template>

<style scoped>
@import "endpointTables.css";
</style>
