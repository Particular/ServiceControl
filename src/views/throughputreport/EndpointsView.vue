<script setup lang="ts">
import DropDown, { type Item } from "@/components/DropDown.vue";
import { computed, onMounted, reactive, ref, watch } from "vue";
import { onBeforeRouteLeave } from "vue-router";
import EndpointThroughputSummary from "@/resources/EndpointThroughputSummary";
import { useShowToast } from "@/composables/toast";
import { TYPE } from "vue-toastification";
import throughputClient from "@/views/throughputreport/throughputClient";
import UpdateUserIndicator from "@/resources/UpdateUserIndicator";

enum DataSource {
  "wellKnownEndpoint" = "ServiceControl",
  "broker" = "Broker",
}

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
  { text: "By source", comparer: (a: EndpointThroughputSummary, b: EndpointThroughputSummary) => (a.is_known_endpoint === b.is_known_endpoint ? 0 : 1) },
].flatMap((item) => {
  return [
    { text: item.text, value: item.text, comparer: item.comparer },
    { text: `${item.text} (Descending)`, value: `${item.text} (Descending)`, comparer: (a, b) => -item.comparer(a, b) },
  ];
});

enum UserIndicator {
  NServiceBusEndpoint = "NServiceBusEndpoint",
  NotNServiceBusEndpoint = "NotNServiceBusEndpoint",
  NServiceBusEndpointSendOnly = "NServiceBusEndpointSendOnly",
  NServiceBusEndpointNoLongerInUse = "NServiceBusEndpointNoLongerInUse",
  TransactionSessionEndpoint = "TransactionSessionEndpoint",
  PlannedToDecommission = "PlannedToDecommission",
}

const userIndicatorMapper = new Map<UserIndicator, string>([
  [UserIndicator.NServiceBusEndpoint, "NServiceBus Endpoint"],
  [UserIndicator.NServiceBusEndpointNoLongerInUse, "No longer in use"],
  [UserIndicator.NServiceBusEndpointSendOnly, "SendOnly Endpoint"],
  [UserIndicator.PlannedToDecommission, "Planned to be decommissioned"],
  [UserIndicator.TransactionSessionEndpoint, "SendOnly Endpoint using Transaction Session"],
  [UserIndicator.NotNServiceBusEndpoint, "Not a NServiceBus Endpoint"],
]);

const endpointTypesForKnownEndpointWithThroughput = [UserIndicator.NServiceBusEndpoint, UserIndicator.TransactionSessionEndpoint, UserIndicator.PlannedToDecommission];
const endpointTypesForKnownEndpointWithZeroThroughput = [UserIndicator.NServiceBusEndpoint, UserIndicator.NServiceBusEndpointSendOnly, UserIndicator.NServiceBusEndpointNoLongerInUse];
const endpointTypesForBroker = [UserIndicator.NServiceBusEndpoint, UserIndicator.NotNServiceBusEndpoint, UserIndicator.TransactionSessionEndpoint, UserIndicator.NServiceBusEndpointNoLongerInUse, UserIndicator.PlannedToDecommission];

let copyOfOriginalDataChanges = new Map<string, { indicator: string }>();
const data = ref<EndpointThroughputSummary[]>([]);
const dataChanges = ref(new Map<string, { indicator: string }>());
const filterData = reactive({ display: "", name: "", nameFilterType: NameFilterType.beginsWith, sort: "" });
const displayFilterData = [
  { value: "", text: "All" },
  { value: DataSource.broker, text: "Broker queues only" },
  { value: DataSource.wellKnownEndpoint, text: "Known Endpoints to ServiceControl" },
];
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
      return !filterData.display || row.is_known_endpoint === (filterData.display === DataSource.wellKnownEndpoint);
    })
    .sort(sortItem?.comparer);
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
  data.value = await throughputClient.endpoints();
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

onBeforeRouteLeave(() => {
  if (hasChanges.value) {
    const answer = window.confirm("You have unsaved changes! Do you want to proceed and lose changes?");
    // cancel the navigation and stay on the same page
    if (!answer) {
      return false;
    }
  }
});

function displayFilterChanged(item: Item) {
  filterData.display = item.value;
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

function getEndpointTypes(row: EndpointThroughputSummary) {
  if (row.is_known_endpoint && row.max_daily_throughput === 0) {
    return endpointTypesForKnownEndpointWithZeroThroughput;
  }
  if (row.is_known_endpoint) {
    return endpointTypesForKnownEndpointWithThroughput;
  }

  return endpointTypesForBroker;
}

async function save() {
  const updateData: UpdateUserIndicator[] = [];
  dataChanges.value.forEach((value, key) => {
    updateData.push({ name: key, user_indicator: value.indicator });
  });

  await throughputClient.updateIndicators(updateData);

  useShowToast(TYPE.INFO, "Saved", "");

  data.value = await throughputClient.endpoints();
}
</script>

<template>
  <div class="box">
    <div class="row">
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
        <drop-down label="Display" :select-item="displayFilterData.find((v) => v.value === filterData.display)" :callback="displayFilterChanged" :items="displayFilterData" />
      </div>
      <div class="col">
        <drop-down label="Sort" :select-item="sortData.find((v) => v.value === filterData.sort)" :callback="sortChanged" :items="sortData" />
      </div>
      <div class="col-1 text-end">
        <button class="btn btn-primary" type="button" @click="save" :disabled="!hasChanges">Save</button>
      </div>
    </div>
    <div class="row results">
      <div class="col format-showing-results">
        <div>Showing {{ filteredData.length }} of {{ data.length }} result(s)</div>
      </div>
      <div class="col"></div>
      <div class="col">
        <div class="dropdown">
          <button class="btn btn-secondary dropdown-toggle" type="button" data-bs-toggle="dropdown" aria-expanded="false">Mark results as</button>
          <ul class="dropdown-menu">
            <li>
              <a href="#" @click.prevent="updateIndicators(UserIndicator.NServiceBusEndpoint)">{{ userIndicatorMapper.get(UserIndicator.NServiceBusEndpoint) }}</a>
              <a href="#" @click.prevent="updateIndicators(UserIndicator.NotNServiceBusEndpoint)">{{ userIndicatorMapper.get(UserIndicator.NotNServiceBusEndpoint) }}</a>
              <a href="#" @click.prevent="updateIndicators(UserIndicator.TransactionSessionEndpoint)">{{ userIndicatorMapper.get(UserIndicator.TransactionSessionEndpoint) }}</a>
              <a href="#" @click.prevent="updateIndicators(UserIndicator.PlannedToDecommission)">{{ userIndicatorMapper.get(UserIndicator.PlannedToDecommission) }}</a>
              <a href="#" @click.prevent="updateIndicators(UserIndicator.NServiceBusEndpointSendOnly)">{{ userIndicatorMapper.get(UserIndicator.NServiceBusEndpointSendOnly) }}</a>
              <a href="#" @click.prevent="updateIndicators(UserIndicator.NServiceBusEndpointNoLongerInUse)">{{ userIndicatorMapper.get(UserIndicator.NServiceBusEndpointNoLongerInUse) }}</a>
            </li>
          </ul>
        </div>
      </div>
      <div class="col-1"></div>
    </div>
  </div>

  <table class="table">
    <thead>
      <tr>
        <th scope="col">Endpoint (Queue)</th>
        <th scope="col">Maximum daily throughput</th>
        <th scope="col">Endpoint Type <i class="fa fa-info-circle info" title="Pick the most correct option" /></th>
      </tr>
    </thead>
    <tbody>
      <tr v-for="row in filteredData" :key="row.name">
        <td class="col">
          <template v-if="!row.is_known_endpoint"><i class="fa fa-cloud-download" aria-hidden="true" title="Discovered from querying broker directly" /></template>
          <template v-else>
            <i class="fa fa-check knownEndpoint" aria-hidden="true" title="Service Control known endpoint" />
          </template>
          {{ row.name }}
        </td>
        <td class="col">{{ row.max_daily_throughput }}</td>
        <td class="col">
          <select class="form-select endpointType format-text" @change="(event) => updateIndicator(event, row.name)">
            <option value="">Pick the most appropriate option</option>
            <option v-for="item in getEndpointTypes(row)" :key="item" :value="item" :selected="(dataChanges.get(row.name)?.indicator ?? getDefaultEndpointType(row)) === item">{{ userIndicatorMapper.get(item) }}</option>
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
.knownEndpoint {
  color: #00c468;
}
.endpointType {
  width: 340px;
}
</style>
