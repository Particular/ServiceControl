<script setup lang="ts" generic="T">
import { ref, computed, watch } from "vue";
import ItemsPerPage from "@/components/ItemsPerPage.vue";
import PaginationStrip from "@/components/PaginationStrip.vue";

const props = withDefaults(
  defineProps<{
    data: T[];
    itemsPerPageOptions?: number[];
    itemsPerPage?: number;
    showPagination?: boolean;
    showItemsPerPage?: boolean;
  }>(),
  { itemsPerPageOptions: () => [20, 35, 50, 75], itemsPerPage: 50, showPagination: true, showItemsPerPage: false }
);

const pageNumber = ref(1);
const itemsPerPage = ref(props.itemsPerPage);
const pageData = computed(() => props.data.slice((pageNumber.value - 1) * itemsPerPage.value, Math.min(pageNumber.value * itemsPerPage.value, props.data.length)));

const emit = defineEmits<{ itemsPerPageChanged: [value: number] }>();

watch(itemsPerPage, () => emit("itemsPerPageChanged", itemsPerPage.value));
</script>

<template>
  <slot name="data" :pageData="pageData" />
  <div class="row">
    <ItemsPerPage v-if="showItemsPerPage" v-model="itemsPerPage" :options="itemsPerPageOptions" />
    <PaginationStrip v-if="showPagination" v-model="pageNumber" :totalCount="data.length" :itemsPerPage="itemsPerPage" />
  </div>
</template>
