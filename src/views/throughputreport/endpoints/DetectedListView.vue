<script setup lang="ts">
import EndpointThroughputSummary from "@/resources/EndpointThroughputSummary";
import { UserIndicator } from "@/views/throughputreport/endpoints/userIndicator";
import DropDown, { type Item } from "@/components/DropDown.vue";
import { computed, onMounted, reactive, ref, watch } from "vue";
import throughputClient from "@/views/throughputreport/throughputClient";
import UpdateUserIndicator from "@/resources/UpdateUserIndicator";
import { TYPE } from "vue-toastification";
import { DataSource } from "@/views/throughputreport/endpoints/dataSource";
import { userIndicatorMapper } from "./userIndicatorMapper";
import ConfirmDialog from "@/components/ConfirmDialog.vue";
import { useShowToast } from "@/composables/toast";
import ResultsCount from "@/components/ResultsCount.vue";
import { useHiddenFeature } from "./useHiddenFeature";
import { license } from "@/composables/serviceLicense";

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
  { text: "name", comparer: (a: EndpointThroughputSummary, b: EndpointThroughputSummary) => a.name.localeCompare(b.name) },
  { text: "throughput", comparer: (a: EndpointThroughputSummary, b: EndpointThroughputSummary) => a.max_daily_throughput - b.max_daily_throughput },
  { text: "endpoint type", comparer: (a: EndpointThroughputSummary, b: EndpointThroughputSummary) => a.user_indicator.localeCompare(b.user_indicator) },
].flatMap((item) => {
  return [
    { text: item.text, value: item.text, comparer: item.comparer },
    { text: `${item.text} (Descending)`, value: `${item.text} (Descending)`, comparer: (a, b) => -item.comparer(a, b) },
  ];
});

export interface DetectedListViewProps {
  ariaLabel: string;
  columnTitle: string;
  showEndpointTypePlaceholder: boolean;
  indicatorOptions: UserIndicator[];
  source: DataSource;
}

const props = defineProps<DetectedListViewProps>();

const data = ref<EndpointThroughputSummary[]>([]);
const dataChanges = ref(new Map<string, { indicator: string }>());
const filterData = reactive({ name: "", nameFilterType: NameFilterType.beginsWith, sort: "name", showUnsetOnly: false });
const filterNameOptions = [
  { text: NameFilterType.beginsWith, filter: (a: EndpointThroughputSummary) => a.name.toLowerCase().startsWith(filterData.name.toLowerCase()) },
  { text: NameFilterType.contains, filter: (a: EndpointThroughputSummary) => a.name.toLowerCase().includes(filterData.name.toLowerCase()) },
  { text: NameFilterType.endsWith, filter: (a: EndpointThroughputSummary) => a.name.toLowerCase().endsWith(filterData.name.toLowerCase()) },
];
const filteredData = computed(() => {
  const sortItem = sortData.find((value) => value.value === filterData.sort);

  return data.value
    .filter((row) => !row.name || filterNameOptions.find((v) => v.text === filterData.nameFilterType)?.filter(row))
    .filter((row) => {
      if (filterData.showUnsetOnly) {
        return dataChanges.value.get(row.name)?.indicator === "";
      }
      return true;
    })
    .sort(sortItem?.comparer);
});
// We can remove this hidden toggle once we have new edition licenses.
const hiddenFeatureToggle = useHiddenFeature(["ArrowUp", "ArrowUp", "ArrowDown", "ArrowDown"]);
const showMonthly = computed(() => license.edition === "MonthlyUsage" || hiddenFeatureToggle.value);

onMounted(async () => {
  await loadData();
});

watch(
  data,
  (value) => {
    dataChanges.value = new Map(value.map((item) => [item.name, { indicator: item.user_indicator }]));
  },
  { deep: true }
);

const showBulkUpdateWarning = ref<boolean>(false);

function cancelChangesWarning() {
  showBulkUpdateWarning.value = false;
}

function proceedWithChangesWarning() {
  showBulkUpdateWarning.value = false;
  updateIndicators();
}

async function loadData() {
  const results = await throughputClient.endpoints();

  data.value = results.filter((row) => row.is_known_endpoint === (props.source === DataSource.WellKnownEndpoint));
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
  save();
}

function showBulkUpdateIndicatorWarning(indicator: UserIndicator) {
  bulkOperation.value = indicator;
  showBulkUpdateWarning.value = true;
}

function updateIndicators() {
  filteredData.value.forEach((item) => {
    updateDataChanged(item.name, (item) => (item.indicator = bulkOperation.value));
  });
  save();
}

function updateDataChanged(name: string, action: (item: { indicator: string }) => void) {
  const item = dataChanges.value.get(name);
  if (item) {
    action(item);
  }
}

function showUnsetChanged(event: Event) {
  filterData.showUnsetOnly = (event.target as HTMLInputElement).checked;
}

const bulkOperation = ref<UserIndicator>(UserIndicator.NServiceBusEndpoint);

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

  useShowToast(TYPE.SUCCESS, "Saved", "", false, { timeout: 1000 });

  await loadData();
}
</script>

<template>
  <div>
    <div class="row filters">
      <div class="col">
        <div class="text-search-container">
          <div>
            <select class="form-select text-search format-text" aria-label="Filter name type" @change="searchTypeChanged">
              <option v-for="item in filterNameOptions" :value="item.text" :key="item.text">{{ item.text }}</option>
            </select>
          </div>
          <div>
            <input type="search" aria-label="Filter by name" class="form-control format-text" :value="filterData.name" @input="nameFilterChanged" placeholder="Filter by name..." />
          </div>
        </div>
      </div>
      <div class="col" style="align-content: center">
        <div>
          <input type="checkbox" aria-label="Show only not set Endpoint Types" class="check-label" id="showUnsetOnly" @input="showUnsetChanged" />
          <label for="showUnsetOnly">Show only not set Endpoint Types</label>
        </div>
      </div>
      <div class="col text-end">
        <drop-down label="Sort by" :select-item="sortData.find((v) => v.value === filterData.sort)" :callback="sortChanged" :items="sortData" />
      </div>
    </div>
  </div>
  <table class="table">
    <tbody>
      <tr>
        <td class="col" colspan="2">
          <ResultsCount :displayed="filteredData.length" :total="data.length" />
        </td>
        <td class="col" style="width: 350px; padding-left: 0">
          <div class="dropdown">
            <button class="btn btn-secondary dropdown-toggle" aria-label="Set Endpoint Type for all items below" :disabled="filteredData.length === 0" type="button" data-bs-toggle="dropdown" aria-expanded="false">
              Set Endpoint Type for all items below
            </button>
            <ul class="dropdown-menu">
              <li v-for="indicator in props.indicatorOptions" :key="indicator">
                <a href="#" :aria-label="userIndicatorMapper.get(indicator)" @click.prevent="showBulkUpdateIndicatorWarning(indicator)">{{ userIndicatorMapper.get(indicator) }}</a>
              </li>
            </ul>
          </div>
        </td>
      </tr>
    </tbody>
  </table>
  <Teleport to="#modalDisplay">
    <ConfirmDialog
      v-if="showBulkUpdateWarning"
      heading="Proceed with bulk operation"
      :body="`Are you sure you want to set ${filteredData.length} endpoints to '${userIndicatorMapper.get(bulkOperation)}'?`"
      @cancel="cancelChangesWarning"
      @confirm="proceedWithChangesWarning"
    />
  </Teleport>
  <table class="table" :aria-label="ariaLabel">
    <thead>
      <tr>
        <th scope="col">{{ props.columnTitle }}</th>
        <th v-if="showMonthly" scope="col" class="text-end formatThroughputColumn">Highest monthly throughput <i class="fa fa-info-circle text-primary" v-tippy="'In the last 12 months'" /></th>
        <th v-else scope="col" class="text-end formatThroughputColumn">Maximum daily throughput <i class="fa fa-info-circle text-primary" v-tippy="'In the last 12 months'" /></th>
        <th scope="col">Endpoint Type <i class="fa fa-info-circle text-primary" v-tippy="'Pick the most correct option'" /></th>
      </tr>
    </thead>
    <tbody>
      <tr v-if="data.length === 0">
        <td colspan="3" class="text-center"><slot name="nodata"></slot></td>
      </tr>
      <tr v-for="row in filteredData" :key="row.name">
        <td class="col" aria-label="name">
          {{ row.name }}
        </td>
        <td v-if="showMonthly" class="col text-end formatThroughputColumn" style="width: 250px" aria-label="maximum usage throughput">{{ row.max_monthly_throughput ? row.max_monthly_throughput.toLocaleString() : "0" }}</td>
        <td v-else class="col text-end formatThroughputColumn" style="width: 250px" aria-label="maximum usage throughput">{{ row.max_daily_throughput.toLocaleString() }}</td>
        <td class="col" style="width: 350px" aria-label="endpoint type">
          <select class="form-select endpointType format-text" @change="(event) => updateIndicator(event, row.name)">
            <option v-if="props.showEndpointTypePlaceholder" value="">Pick the most appropriate option</option>
            <option v-for="item in props.indicatorOptions" :key="item" :value="item" :selected="(dataChanges.get(row.name)?.indicator ?? getDefaultEndpointType(row)) === item">{{ userIndicatorMapper.get(item) }}</option>
          </select>
        </td>
      </tr>
    </tbody>
  </table>
</template>

<style scoped>
.formatThroughputColumn {
  padding-right: 20px;
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
