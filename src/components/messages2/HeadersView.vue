<script setup lang="ts">
import CopyToClipboard from "@/components/CopyToClipboard.vue";
import { computed, ref } from "vue";
import { useMessageStore } from "@/stores/MessageStore";
import { storeToRefs } from "pinia";
import FilterInput from "@/components/FilterInput.vue";

const { headers } = storeToRefs(useMessageStore());

const hoverStates = ref<Record<number, boolean>>({});
const searchTerm = ref<string>("");
const toggleHover = (index: number, state: boolean) => {
  hoverStates.value[index] = state;
};
// Computed property to filter headers based on search term
const filteredHeaders = computed(() => {
  if (!searchTerm.value) {
    return headers.value.data;
  }
  return headers.value.data.filter((header) => header.key.toLowerCase().includes(searchTerm.value.toLowerCase()) || header.value?.toLowerCase().includes(searchTerm.value.toLowerCase()));
});
</script>

<template>
  <div>
    <div class="row filters">
      <div class="col">
        <div class="text-search-container">
          <div class="text-search">
            <div>
              <FilterInput v-model="searchTerm" :aria-label="`Search for a header key or value`" :placeholder="'Search for a header key or value...'" />
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
  <div class="header-list" v-if="filteredHeaders.length > 0 && !headers.not_found">
    <template v-for="(header, index) in filteredHeaders" :key="index">
      <div class="header-key">{{ header.key }}</div>
      <div class="header-value" @mouseover="toggleHover(index, true)" @mouseleave="toggleHover(index, false)">
        <pre class="removeBootStrap">{{ header.value }}</pre>
        <div class="clippy-button"><CopyToClipboard v-if="header.value && hoverStates[index]" :value="header.value" :isIconOnly="true" /></div>
      </div>
    </template>
  </div>

  <!-- Message if filtered list is empty -->
  <div v-if="filteredHeaders.length <= 0 && !headers.not_found" class="alert alert-warning">No headers found matching the search term.</div>
  <div v-if="headers.not_found" class="alert alert-info">Could not find message headers. This could be because the message URL is invalid or the corresponding message was processed and is no longer tracked by ServiceControl.</div>
</template>

<style scoped>
.removeBootStrap {
  background: initial;
  border: none;
  margin: 0;
  padding: 0;
}

/*  empty filtered list message */
.alert-warning {
  margin-top: 10px;
  color: #856404;
  background-color: #fff3cd;
  border-color: #ffeeba;
  padding: 10px;
  border-radius: 5px;
}

.text-search-container {
  display: flex;
  flex-direction: row;
}
.text-search {
  width: 100%;
  max-width: 40rem;
}
.filters {
  background-color: #f3f3f3;
  margin-top: 5px;
  border: #8c8c8c 1px solid;
  border-radius: 3px;
  padding: 5px;
}

.header-list {
  margin-bottom: 1rem;
  margin-top: 0.5rem;
  display: grid;
  grid-template-columns: 20rem 1fr;
  align-items: flex-start;
  justify-content: center;
  row-gap: 2px;
}

.header-value,
.header-key {
  padding: 1rem;
  display: flex;
  height: 100%;
  min-height: 2rem;
  position: relative;
  background: white;
}

.header-value {
  padding-left: 2.7rem;
}

.clippy-button {
  position: absolute;
  left: 0;
  top: 0.3rem;
}
</style>
