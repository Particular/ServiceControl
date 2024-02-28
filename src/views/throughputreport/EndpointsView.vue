<script setup lang="ts">
import DropDown, { type Item } from "@/components/DropDown.vue";
import { computed, reactive } from "vue";
import { data } from "@/views/throughputreport/randomData";
import { onBeforeRouteLeave } from "vue-router";

enum DataSource {
  "wellKnownEndpoint" = "ServiceControl",
  "broker" = "Broker",
}

enum NameFilterType {
  beginsWith = "Begins with",
  contains = "Contains",
  endsWith = "Ends with",
}

interface ThroughputEndpoint {
  source: DataSource;
  name: string;
  throughputValue: number;
  sendOnly?: boolean;
  doNotInclude?: boolean;
}

interface SortData {
  text: string;
  value: string;
  comparer: (a: ThroughputEndpoint, b: ThroughputEndpoint) => number;
}

const sortData: SortData[] = [
  { text: "By name", comparer: (a: ThroughputEndpoint, b: ThroughputEndpoint) => a.name.localeCompare(b.name) },
  { text: "By throughput", comparer: (a: ThroughputEndpoint, b: ThroughputEndpoint) => a.throughputValue - b.throughputValue },
  { text: "By source", comparer: (a: ThroughputEndpoint, b: ThroughputEndpoint) => a.source.localeCompare(b.source) },
].flatMap((item) => {
  return [
    { text: item.text, value: item.text, comparer: item.comparer },
    { text: `${item.text} (Descending)`, value: `${item.text} (Descending)`, comparer: (a, b) => -item.comparer(a, b) },
  ];
});

let copyOfOriginalDataChanges: Map<string, { sendOnly: boolean; doNotInclude: true }>;

const dataChanges = reactive(new Map(data.map((item) => [item.name, { sendOnly: item.sendOnly, doNotInclude: item.doNotInclude }])));
const filterData = reactive({ display: "", name: "", nameFilterType: NameFilterType.beginsWith, sort: "" });

updateCopyOfOriginalDataChanges();

function updateCopyOfOriginalDataChanges() {
  // We need to do a deep copy, see https://stackoverflow.com/a/56853666
  copyOfOriginalDataChanges = new Map(JSON.parse(JSON.stringify(Array.from(dataChanges))));
}
function displayFilterChanged(item: Item) {
  filterData.display = item.value;
}

function nameFilterChanged(event: Event) {
  filterData.name = (event.target as HTMLInputElement).value;
}

const displayFilterData = [
  { value: "", text: "All" },
  { value: DataSource.broker, text: "Broker queues only" },
  { value: DataSource.wellKnownEndpoint, text: "Known Endpoints to ServiceControl" },
];

const filterNameOptions = [
  { text: NameFilterType.beginsWith, filter: (a: ThroughputEndpoint) => a.name.toLowerCase().startsWith(filterData.name.toLowerCase()) },
  { text: NameFilterType.contains, filter: (a: ThroughputEndpoint) => a.name.toLowerCase().includes(filterData.name.toLowerCase()) },
  { text: NameFilterType.endsWith, filter: (a: ThroughputEndpoint) => a.name.toLowerCase().endsWith(filterData.name.toLowerCase()) },
];
const filteredData = computed(() => {
  const sortItem = sortData.find((value) => value.value === filterData.sort);

  return (data as ThroughputEndpoint[])
    .filter((row) => !row.name || filterNameOptions.find((v) => v.text === filterData.nameFilterType)?.filter(row))
    .filter((row) => {
      return !filterData.display || row.source === filterData.display;
    })
    .sort(sortItem?.comparer);
});

function sortChanged(item: Item) {
  filterData.sort = item.value;
}

function searchTypeChanged(event: Event) {
  filterData.nameFilterType = (event.target as HTMLInputElement).value as NameFilterType;
}

function markResultsAsDoNotInclude(value: boolean) {
  filteredData.value.forEach((item) => {
    updateDataChanged(item.name, (item) => (item.doNotInclude = value));
  });
}

function markResultsAsDoSendOnly(value: boolean) {
  filteredData.value.forEach((item) => {
    updateDataChanged(item.name, (item) => (item.sendOnly = value));
  });
}

function updateSendOnlyCheckbox(event: Event, name: string) {
  const checked = (event.target as HTMLInputElement).checked;

  updateDataChanged(name, (item) => (item.sendOnly = checked));
}

function updateDoNotIncludeCheckbox(event: Event, name: string) {
  const checked = (event.target as HTMLInputElement).checked;

  updateDataChanged(name, (item) => (item.doNotInclude = checked));
}

function updateDataChanged(name: string, action: (item: { doNotInclude: boolean; sendOnly: boolean }) => void) {
  const item = dataChanges.get(name);
  if (item) {
    action(item);
  }
}

onBeforeRouteLeave(() => {
  function anyChanges() {
    if (copyOfOriginalDataChanges.size !== dataChanges.size) {
      return true;
    }
    for (const [key, { sendOnly, doNotInclude }] of copyOfOriginalDataChanges) {
      const a = dataChanges.get(key);

      if (a === undefined) {
        return true;
      }

      if (a.sendOnly !== sendOnly || a.doNotInclude !== doNotInclude) {
        return true;
      }
    }

    return false;
  }

  if (anyChanges()) {
    const answer = window.confirm("You have unsaved changes! Do you want to proceed and lose changes?");
    // cancel the navigation and stay on the same page
    if (!answer) {
      return false;
    }
  }
});

function save() {
  //TODO: Save data to backend
  updateCopyOfOriginalDataChanges();
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
            <input type="text" class="form-control format-text" :value="filterData.name" @input="nameFilterChanged" placeholder="Filter by name..." />
          </div>
        </div>
      </div>
      <div class="col">
        <drop-down label="Display" :select-item="displayFilterData.find((v) => v.value === filterData.display)" :callback="displayFilterChanged" :items="displayFilterData" />
      </div>
      <div class="col">
        <drop-down label="Sort" :select-item="sortData.find((v) => v.value === filterData.sort)" :callback="sortChanged" :items="sortData" />
      </div>
      <div class="col-1">
        <button class="btn btn-primary" type="button" @click="save">Save</button>
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
            <li><a class="dropdown-item" href="#" @click.prevent="() => markResultsAsDoNotInclude(true)">Do not include</a></li>
            <li><a class="dropdown-item" href="#" @click.prevent="() => markResultsAsDoNotInclude(false)">Include</a></li>
            <li><a class="dropdown-item" href="#" @click.prevent="() => markResultsAsDoSendOnly(true)">Send only</a></li>
            <li><a class="dropdown-item" href="#" @click.prevent="() => markResultsAsDoSendOnly(false)">Not Send only</a></li>
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
        <th scope="col">Max daily throughput<br />this month</th>
        <th scope="col">Is Send only <i class="fa fa-info-circle info" title="Queues with 0 throughput are candidates to be send only endpoint." /></th>
        <th scope="col">Do not include <i class="fa fa-info-circle info" title="What queues are part of NServiceBus" /></th>
      </tr>
    </thead>
    <tbody>
      <tr v-for="row in filteredData" :key="row.name">
        <td class="col">
          <template v-if="row.source === DataSource.broker"><i class="fa fa-cloud-download" aria-hidden="true" title="Discovered from querying broker directly" /></template>
          <template v-else>
            <i class="fa fa-check knownEndpoint" aria-hidden="true" title="Service Control known endpoint" />
          </template>
          {{ row.name }}
        </td>
        <td class="col">{{ row.throughputValue }}</td>
        <td class="col">
          <div v-if="row.throughputValue === 0" class="form-check">
            <input class="form-check-input" type="checkbox" :checked="dataChanges.get(row.name)?.sendOnly" @change="(e) => updateSendOnlyCheckbox(e, row.name)" :id="`sendonly[${row.name}]`" /><label
              class="form-check-label override-font"
              :for="`sendonly[${row.name}]`"
              >Send only</label
            >
          </div>
        </td>
        <td class="col">
          <div v-if="row.source !== DataSource.wellKnownEndpoint" class="form-check">
            <input class="form-check-input" type="checkbox" :checked="dataChanges.get(row.name)?.doNotInclude" @change="(e) => updateDoNotIncludeCheckbox(e, row.name)" :id="`doNotInclude[${row.name}]`" /><label
              class="form-check-label override-font"
              :for="`doNotInclude[${row.name}]`"
              >Do not include</label
            >
          </div>
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
</style>
