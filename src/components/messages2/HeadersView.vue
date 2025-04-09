<script setup lang="ts">
import CopyToClipboard from "@/components/CopyToClipboard.vue";
import { computed, ref } from "vue";
import { useMessageStore } from "@/stores/MessageStore";
import { storeToRefs } from "pinia";

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
            <input type="search" aria-label="Filter by name" v-model="searchTerm" class="form-control format-text" placeholder="Search for a header key or value..." />
          </div>
        </div>
      </div>
    </div>
  </div>
  <table class="table" v-if="filteredHeaders.length > 0 && !headers.not_found">
    <tbody>
      <tr class="interactiveList" v-for="(header, index) in filteredHeaders" :key="index">
        <td nowrap="nowrap">{{ header.key }}</td>
        <td>
          <div class="headercopy" @mouseover="toggleHover(index, true)" @mouseleave="toggleHover(index, false)">
            <pre>{{ header.value }}</pre>
            <CopyToClipboard v-if="hoverStates[index] && header.value" :value="header.value" :isIconOnly="true" />
          </div>
        </td>
      </tr>
    </tbody>
  </table>

  <!-- Message if filtered list is empty -->
  <div v-if="filteredHeaders.length <= 0 && !headers.not_found" class="alert alert-warning">No headers found matching the search term.</div>
  <div v-if="headers.not_found" class="alert alert-info">Could not find message headers. This could be because the message URL is invalid or the corresponding message was processed and is no longer tracked by ServiceControl.</div>
</template>

<style scoped>
.headercopy {
  display: flex;
  gap: 0.4rem;
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
.format-text {
  font-weight: unset;
  font-size: 14px;
  min-width: 120px;
}
.filters {
  background-color: #f3f3f3;
  margin-top: 5px;
  border: #8c8c8c 1px solid;
  border-radius: 3px;
  padding: 5px;
}
</style>
