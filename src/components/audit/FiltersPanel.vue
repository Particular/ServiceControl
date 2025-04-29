<script setup lang="ts">
import FilterInput from "@/components/FilterInput.vue";
import { storeToRefs } from "pinia";
import { FieldNames, useAuditStore } from "@/stores/AuditStore.ts";
import ListFilterSelector from "@/components/audit/ListFilterSelector.vue";
import { computed, useTemplateRef } from "vue";
import DatePickerRange from "@/components/audit/DatePickerRange.vue";
import { Tippy, TippyComponent } from "vue-tippy";

const store = useAuditStore();
const { sortBy, messageFilterString, selectedEndpointName, endpoints, itemsPerPage, dateRange } = storeToRefs(store);
const endpointNames = computed(() => {
  return [...new Set(endpoints.value.map((endpoint) => endpoint.name))].sort();
});
const wildcardTooltipRef = useTemplateRef<TippyComponent | null>("wildcardTooltipRef");
const sortByItemsMap = new Map([
  ["Latest sent", `${FieldNames.TimeSent},desc`],
  ["Oldest sent", `${FieldNames.TimeSent},asc`],
  ["Slowest processing time", `${FieldNames.ProcessingTime},desc`],
  ["Highest critical time", `${FieldNames.CriticalTime},desc`],
  ["Longest delivery time", `${FieldNames.DeliveryTime},desc`],
]);
const numberOfItemsPerPage = ["50", "100", "250", "500"];
const sortByItems = computed(() => [...sortByItemsMap.keys()]);
const selectedSortByItem = computed({
  get() {
    return findKeyByValue(`${sortBy.value.property},${sortBy.value.isAscending ? "asc" : "desc"}`);
  },
  set(newValue) {
    const item = sortByItemsMap.get(newValue);
    if (item) {
      const strings = item.split(",");
      sortBy.value = { isAscending: strings[1] === "asc", property: strings[0] };
    } else {
      sortBy.value = { isAscending: true, property: FieldNames.TimeSent };
    }
  },
});
const selectedItemsPerPage = computed({
  get() {
    return itemsPerPage.value.toString();
  },
  set(newValue) {
    itemsPerPage.value = parseInt(newValue);
  },
});

function findKeyByValue(searchValue: string) {
  for (const [key, value] of sortByItemsMap.entries()) {
    if (value === searchValue) {
      return key;
    }
  }
  return "";
}

function toggleWildcardToolTip(show: boolean) {
  if (show) {
    wildcardTooltipRef.value?.show();
  } else {
    wildcardTooltipRef.value?.hide();
  }
}
</script>

<template>
  <div class="filters">
    <div class="filter">
      <div class="filter-label"></div>
      <div class="filter-component text-search-container">
        <Tippy ref="wildcardTooltipRef" trigger="click" :hideOnClick="false">
          <template #content>
            <h4>Use <i class="fa fa-asterisk asterisk" /> to do wildcard searches <i class="fa fa-lightbulb-o" style="color: #e6c201" /></h4>
            <p>
              Example: <i><i class="fa fa-asterisk asterisk" />World!</i> or <i>Hello<i class="fa fa-asterisk asterisk" /></i>, to look for <i>Hello World!</i>
            </p>
          </template>
          <FilterInput v-model="messageFilterString" placeholder="Search messages..." aria-label="Search messages" @focus="() => toggleWildcardToolTip(true)" @blur="() => toggleWildcardToolTip(false)" />
        </Tippy>
      </div>
    </div>
    <div class="filter">
      <div class="filter-label">Endpoint:</div>
      <div class="filter-component">
        <ListFilterSelector :items="endpointNames" instructions="Select an endpoint" v-model="selectedEndpointName" item-name="endpoint" label="Endpoint" default-empty-text="Any" :show-clear="true" :show-filter="true" />
      </div>
    </div>
    <div class="filter">
      <div class="filter-label">Dates:</div>
      <div class="filter-component">
        <DatePickerRange v-model="dateRange" />
      </div>
    </div>
    <div class="filter">
      <div class="filter-label">Show:</div>
      <div class="filter-component">
        <ListFilterSelector :items="numberOfItemsPerPage" instructions="Select how many result to display" v-model="selectedItemsPerPage" item-name="result" :can-clear="false" :show-clear="false" :show-filter="false" />
      </div>
    </div>
    <div class="filter">
      <div class="filter-label">Sort:</div>
      <div class="filter-component">
        <ListFilterSelector :items="sortByItems" v-model="selectedSortByItem" item-name="result" :can-clear="false" :show-clear="false" :show-filter="false" />
      </div>
    </div>
  </div>
</template>

<style scoped>
.asterisk {
  color: #04b9ff;
  font-size: 1.2rem;
}
.filters {
  background-color: #f3f3f3;
  border: #8c8c8c 1px solid;
  border-radius: 3px;
  padding: 0.3125rem;
  display: flex;
  gap: 1.1rem;
  flex-wrap: wrap;
}

.filter {
  display: flex;
  align-items: center;
}

.filter:last-child {
  flex-grow: 1;
  place-content: flex-end;
}

.filter-label {
  font-weight: bold;
}

.text-search-container {
  width: 25rem;
}
</style>
