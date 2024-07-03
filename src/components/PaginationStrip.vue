<script setup lang="ts">
import { computed, watch } from "vue";

const props = withDefaults(
  defineProps<{
    itemsPerPage: number;
    totalCount: number;
    pageBuffer?: number;
  }>(),
  { pageBuffer: 5 }
);

const pageNumber = defineModel<number>({ required: true });

const numberOfPages = computed(() => {
  return Math.ceil(props.totalCount / props.itemsPerPage);
});

const showPagination = computed(() => {
  return numberOfPages.value > 1;
});

const doublePageBuffer = computed(() => props.pageBuffer * 2);

watch(numberOfPages, (newValue) => {
  if (newValue < pageNumber.value) {
    pageNumber.value = 1;
  }
});

interface PageData {
  label: string;
  page: number;
  key: string;
  class?: {
    disabled?: boolean;
    active?: boolean;
  };
}
const pages = computed(() => {
  const pages: PageData[] = [];
  pages.push({
    label: "Previous",
    page: Math.max(pageNumber.value - 1, 1),
    key: "Previous Page",
    class: {
      disabled: pageNumber.value === 1,
    },
  });

  if (pageNumber.value > props.pageBuffer + 1 && numberOfPages.value >= doublePageBuffer.value) {
    pages.push(
      {
        label: "1",
        page: 1,
        key: "First Page",
      },
      {
        label: "...",
        page: pageNumber.value - props.pageBuffer,
        key: `Back ${props.pageBuffer}`,
      }
    );
  }

  let startIndex = pageNumber.value - props.pageBuffer;
  let endIndex = pageNumber.value + props.pageBuffer;
  if (startIndex < 1) {
    // Increase the end index by the offset
    endIndex -= startIndex;
    startIndex = 1;
  }

  let showEnd = false;
  if (endIndex >= numberOfPages.value) {
    endIndex = numberOfPages.value;
    showEnd = true;
  }

  // All of the surrounding pages
  for (let n = startIndex; n <= endIndex; n++) {
    pages.push({
      label: `${n}`,
      page: n,
      key: `Page ${n}`,
      class: {
        active: n === pageNumber.value,
      },
    });
  }

  if (!showEnd) {
    pages.push(
      {
        label: "...",
        page: pageNumber.value + props.pageBuffer,
        key: `Forward ${props.pageBuffer}`,
      },
      {
        label: `${numberOfPages.value}`,
        page: numberOfPages.value,
        key: "Last Page",
      }
    );
  }

  pages.push({
    label: "Next",
    page: Math.min(pageNumber.value + 1, numberOfPages.value),
    key: "Next Page",
    class: {
      disabled: pageNumber.value === numberOfPages.value,
    },
  });

  return pages;
});
</script>

<template>
  <div v-if="showPagination" class="col align-self-center">
    <ul aria-label="pagination" class="pagination justify-content-center">
      <li v-for="page of pages" class="page-item" :key="page.key">
        <button :aria-pressed="page.class?.active" :disabled="page.class?.disabled" :aria-label="page.key" class="page-link" @click="pageNumber = page.page" :class="page.class">{{ page.label }}</button>
      </li>
    </ul>
  </div>
</template>

<style scoped>
.page-link {
  cursor: pointer;
}

.page-link.disabled {
  cursor: not-allowed;
  pointer-events: all !important;
}
</style>
