<script setup lang="ts">
import EndpointListColumnHeader from "./EndpointListColumnHeader.vue";
import EndpointListRow, { columnName } from "./EndpointListRow.vue";
import { useMonitoringStore } from "@/stores/MonitoringStore";
import { storeToRefs } from "pinia";

const monitoringStore = useMonitoringStore();
const { sortBy: activeColumn } = storeToRefs(monitoringStore);

type EndpointListColumn = {
  name: columnName;
  displayName: string;
  displayUnit?: string;
  sort: string;
  toolTip?: string;
};

const columns: EndpointListColumn[] = [
  {
    name: columnName.ENDPOINTNAME,
    displayName: "Endpoint name",
    sort: columnName.ENDPOINTNAME,
  },
  {
    name: columnName.QUEUELENGTH,
    displayName: "Queue Length",
    displayUnit: "(MSGS)",
    sort: monitoringStore.endpointListIsGrouped ? "" : columnName.QUEUELENGTH,
    toolTip: "Queue length: The number of messages waiting to be processed in the input queue(s) of the endpoint.",
  },
  {
    name: columnName.THROUGHPUT,
    displayName: "Throughput",
    displayUnit: "(msgs/s)",
    sort: monitoringStore.endpointListIsGrouped ? "" : columnName.THROUGHPUT,
    toolTip: "Throughput: The number of messages per second successfully processed by a receiving endpoint.",
  },
  {
    name: columnName.SCHEDULEDRETRIES,
    displayName: "Scheduled retries",
    displayUnit: "(msgs/s)",
    sort: monitoringStore.endpointListIsGrouped ? "" : columnName.SCHEDULEDRETRIES,
    toolTip: "Scheduled retries: The number of messages per second scheduled for retries (immediate or delayed).",
  },
  {
    name: columnName.PROCESSINGTIME,
    displayName: "Processing Time",
    displayUnit: "(t)",
    sort: monitoringStore.endpointListIsGrouped ? "" : columnName.PROCESSINGTIME,
    toolTip: "Processing time: The time taken for a receiving endpoint to successfully process a message.",
  },
  {
    name: columnName.CRITICALTIME,
    displayName: "Critical Time",
    displayUnit: "(t)",
    sort: monitoringStore.endpointListIsGrouped ? "" : columnName.CRITICALTIME,
    toolTip: "Critical time: The elapsed time from when a message was sent, until it was successfully processed by a receiving endpoint.",
  },
];
</script>

<template>
  <section role="table" aria-label="endpoint-list">
    <!--Table headings-->
    <div role="row" aria-label="column-headers" class="table-head-row">
      <EndpointListColumnHeader
        v-for="(column, i) in columns"
        :key="column.name"
        :columnName="column.name"
        :columnSort="column.sort"
        :displayName="column.displayName"
        :displayUnit="column.displayUnit"
        :toolTip="column.toolTip"
        :isFirstCol="i === 0"
        v-model="activeColumn"
      />
    </div>
    <div>
      <div v-if="monitoringStore.endpointListIsGrouped" role="rowgroup" aria-label="grouped-endpoints">
        <div role="row" :aria-labelledby="endpointGroup.group" class="row" v-for="endpointGroup in monitoringStore.grouping.groupedEndpoints" :key="endpointGroup.group">
          <div role="rowheader" class="endpoint-group-title" :id="endpointGroup.group">
            {{ endpointGroup.group }}
          </div>
          <div role="group" :aria-labelledby="endpointGroup.group">
            <div role="row" :aria-label="groupedEndpoint.shortName" aria-description="endpoint-row" class="row box endpoint-row" v-for="groupedEndpoint in endpointGroup.endpoints" :key="groupedEndpoint.endpoint.name">
              <EndpointListRow :endpoint="groupedEndpoint" />
            </div>
          </div>
        </div>
      </div>
      <div v-else role="rowgroup" aria-label="ungrouped-endpoints">
        <div role="row" :aria-label="endpoint.name" class="endpoint-row" v-for="endpoint in monitoringStore.getEndpointList" :key="endpoint.name">
          <EndpointListRow :endpoint="endpoint" />
        </div>
      </div>
    </div>
  </section>
</template>

<style scoped>
@import "./endpoint.css";
@import "./endpointTables.css";

.endpoint-group-title {
  font-size: 14px;
  font-weight: bold;
  margin: 20px 0 10px 15px;
}
</style>
