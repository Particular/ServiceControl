<script setup lang="ts">
import EndpointThroughputSummary from "@/resources/EndpointThroughputSummary";
import { UserIndicator } from "@/views/throughputreport/endpoints/userIndicator";
import DropDown, { type Item } from "@/components/DropDown.vue";
import { computed, onMounted, reactive, ref, watch } from "vue";
import throughputClient from "@/views/throughputreport/throughputClient";
import { onBeforeRouteLeave } from "vue-router";
import UpdateUserIndicator from "@/resources/UpdateUserIndicator";
import { useShowToast } from "@/composables/toast";
import { TYPE } from "vue-toastification";
import { DataSource } from "@/views/throughputreport/endpoints/dataSource";
import { userIndicatorMapper } from "./userIndicatorMapper";
import ConfirmDialog from "@/components/ConfirmDialog.vue";
import NavigateAway from "@/views/throughputreport/endpoints/navigateAway";

enum NameFilterType {
  beginsWith = "Begins with",
  contains = "Contains",
  endsWith = "Ends with",
}

interface SortData {
  text: string;
  value: string;
  comparer: (a: EndpointThroughputSummary, b: EndpointThroughputSummary) => number;
}

const sortData: SortData[] = [
  { text: "By name", comparer: (a: EndpointThroughputSummary, b: EndpointThroughputSummary) => a.name.localeCompare(b.name) },
  { text: "By throughput", comparer: (a: EndpointThroughputSummary, b: EndpointThroughputSummary) => a.max_daily_throughput - b.max_daily_throughput },
  { text: "By endpoint type", comparer: (a: EndpointThroughputSummary, b: EndpointThroughputSummary) => a.user_indicator.localeCompare(b.user_indicator) },
].flatMap((item) => {
  return [
    { text: item.text, value: item.text, comparer: item.comparer },
    { text: `${item.text} (Descending)`, value: `${item.text} (Descending)`, comparer: (a, b) => -item.comparer(a, b) },
  ];
});

const props = defineProps<{
  columnTitle: string;
  indicatorOptions: UserIndicator[];
  source: DataSource;
}>();

let copyOfOriginalDataChanges = new Map<string, { indicator: string }>();
const data = ref<EndpointThroughputSummary[]>([]);
const dataChanges = ref(new Map<string, { indicator: string }>());
const filterData = reactive({ name: "", nameFilterType: NameFilterType.beginsWith, sort: "By name" });
const filterNameOptions = [
  { text: NameFilterType.beginsWith, filter: (a: EndpointThroughputSummary) => a.name.toLowerCase().startsWith(filterData.name.toLowerCase()) },
  { text: NameFilterType.contains, filter: (a: EndpointThroughputSummary) => a.name.toLowerCase().includes(filterData.name.toLowerCase()) },
  { text: NameFilterType.endsWith, filter: (a: EndpointThroughputSummary) => a.name.toLowerCase().endsWith(filterData.name.toLowerCase()) },
];
const filteredData = computed(() => {
  const sortItem = sortData.find((value) => value.value === filterData.sort);

  return data.value.filter((row) => !row.name || filterNameOptions.find((v) => v.text === filterData.nameFilterType)?.filter(row)).sort(sortItem?.comparer);
});
const hasChanges = computed(() => {
  if (dataChanges.value.size === 0) {
    return false;
  }
  if (copyOfOriginalDataChanges.size !== dataChanges.value.size) {
    return true;
  }
  for (const [key, { indicator }] of copyOfOriginalDataChanges) {
    const a = dataChanges.value.get(key);

    if (a === undefined) {
      return true;
    }

    if (a.indicator !== indicator) {
      return true;
    }
  }

  return false;
});

onMounted(async () => {
  await loadData();
});

watch(
  data,
  (value) => {
    dataChanges.value = new Map(value.map((item) => [item.name, { indicator: item.user_indicator }]));
    // We need to do a deep copy, see https://stackoverflow.com/a/56853666
    copyOfOriginalDataChanges = new Map(JSON.parse(JSON.stringify(Array.from(dataChanges.value))));
  },
  { deep: true }
);

const showLoseChangesWarning = ref<boolean>(false);
const navigateAway = new NavigateAway();

onBeforeRouteLeave(() => {
  if (hasChanges.value) {
    showLoseChangesWarning.value = true;
    return navigateAway.navigateGuard();
  }
});

function hideLoseChangesWarning() {
  navigateAway.cancel();
  showLoseChangesWarning.value = false;
}

function proceedWithLoseChangesWarning() {
  navigateAway.proceed();
  showLoseChangesWarning.value = false;
}

async function loadData() {
  const results = await throughputClient.endpoints();

  data.value = results.filter((row) => row.is_known_endpoint === (props.source === DataSource.wellKnownEndpoint));
}
function nameFilterChanged(event: Event) {
  filterData.name = (event.target as HTMLInputElement).value;
}

function sortChanged(item: Item) {
  filterData.sort = item.value;
}

function searchTypeChanged(event: Event) {
  filterData.nameFilterType = (event.target as HTMLInputElement).value as NameFilterType;
}

function updateIndicator(event: Event, name: string) {
  const value = (event.target as HTMLSelectElement).value;
  updateDataChanged(name, (item) => (item.indicator = value));
}

function updateIndicators(indicator: UserIndicator) {
  filteredData.value.forEach((item) => {
    updateDataChanged(item.name, (item) => (item.indicator = indicator));
  });
}

function updateDataChanged(name: string, action: (item: { indicator: string }) => void) {
  const item = dataChanges.value.get(name);
  if (item) {
    action(item);
  }
}

function getDefaultEndpointType(row: EndpointThroughputSummary) {
  if (row.is_known_endpoint) {
    return UserIndicator.NServiceBusEndpoint.toString();
  }

  return undefined;
}

async function save() {
  const updateData: UpdateUserIndicator[] = [];
  dataChanges.value.forEach((value, key) => {
    updateData.push({ name: key, user_indicator: value.indicator });
  });

  await throughputClient.updateIndicators(updateData);

  useShowToast(TYPE.INFO, "Saved", "");

  await loadData();
}
</script>

<template>
  <div>
    <div class="row filters">
      <div class="col">
        <div class="text-search-container">
          <div>
            <select class="form-select text-search format-text" @change="searchTypeChanged">
              <option v-for="item in filterNameOptions" :value="item.text" :key="item.text">{{ item.text }}</option>
            </select>
          </div>
          <div>
            <input type="search" class="form-control format-text" :value="filterData.name" @input="nameFilterChanged" placeholder="Filter by name..." />
          </div>
        </div>
      </div>
      <div class="col">
        <drop-down label="Sort" :select-item="sortData.find((v) => v.value === filterData.sort)" :callback="sortChanged" :items="sortData" />
      </div>
      <div class="col">
        <div class="dropdown">
          <button class="btn btn-secondary dropdown-toggle" type="button" data-bs-toggle="dropdown" aria-expanded="false">Set displayed Endpoint Types to</button>
          <ul class="dropdown-menu">
            <li v-for="indicator in props.indicatorOptions" :key="indicator">
              <a href="#" @click.prevent="updateIndicators(indicator)">{{ userIndicatorMapper.get(indicator) }}</a>
            </li>
          </ul>
        </div>
      </div>
      <div class="col-1 text-end">
        <button class="btn btn-primary" type="button" @click="save" :disabled="!hasChanges">Save</button>
      </div>
    </div>
    <div class="row results">
      <div class="col format-showing-results">
        <div>Showing {{ filteredData.length }} of {{ data.length }} result(s)</div>
      </div>
    </div>
  </div>
  <Teleport to="#modalDisplay">
    <ConfirmDialog v-if="showLoseChangesWarning" heading="You have unsaved changes" body="Do you want to proceed and lose changes??" @cancel="hideLoseChangesWarning" @confirm="proceedWithLoseChangesWarning" />
  </Teleport>
  <table class="table">
    <thead>
      <tr>
        <th scope="col">{{ props.columnTitle }}</th>
        <th scope="col">Maximum daily throughput</th>
        <th scope="col">Endpoint Type <i class="fa fa-info-circle info" v-tooltip title="Pick the most correct option" /></th>
      </tr>
    </thead>
    <tbody>
      <tr v-if="data.length === 0">
        <td colspan="2" class="text-center"><slot name="nodata"></slot></td>
      </tr>
      <tr v-for="row in filteredData" :key="row.name">
        <td class="col">
          {{ row.name }}
        </td>
        <td class="col" style="width: 250px">{{ row.max_daily_throughput }}</td>
        <td class="col" style="width: 350px">
          <select class="form-select endpointType format-text" @change="(event) => updateIndicator(event, row.name)">
            <option value="">Pick the most appropriate option</option>
            <option v-for="item in props.indicatorOptions" :key="item" :value="item" :selected="(dataChanges.get(row.name)?.indicator ?? getDefaultEndpointType(row)) === item">{{ userIndicatorMapper.get(item) }}</option>
          </select>
        </td>
      </tr>
    </tbody>
  </table>
</template>

<style scoped>
.format-showing-results {
  display: flex;
  align-items: flex-end;
}
.results {
  margin-top: 5px;
}
.format-text {
  font-weight: unset;
  font-size: 14px;
  min-width: 120px;
}
.text-search-container {
  display: flex;
  flex-direction: row;
}
.text-search {
  width: 130px;
}
.info {
  color: dodgerblue;
}
.endpointType {
  width: 340px;
}
.filters {
  background-color: #f3f3f3;
  margin-top: 5px;
  border: #8c8c8c 1px solid;
  border-radius: 3px;
  padding: 5px;
}
</style>
