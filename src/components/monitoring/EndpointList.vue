<script setup lang="ts">
import SortableColumn from "../../components/SortableColumn.vue";
import EndpointListRow, { columnName } from "./EndpointListRow.vue";
import { useMonitoringStore } from "@/stores/MonitoringStore";
import { storeToRefs } from "pinia";

const monitoringStore = useMonitoringStore();
const { sortBy: activeColumn } = storeToRefs(monitoringStore);
</script>

<template>
  <section role="table" aria-label="endpoint-list">
    <!--Table headings-->
    <div role="row" aria-label="column-headers" class="table-head-row">
      <div role="columnheader" :aria-label="columnName.ENDPOINTNAME" class="table-first-col">
        <SortableColumn :sort-by="columnName.ENDPOINTNAME" v-model="activeColumn" :default-ascending="true">Endpoint name</SortableColumn>
      </div>
      <div role="columnheader" :aria-label="columnName.QUEUELENGTH" class="table-col">
        <SortableColumn :sort-by="monitoringStore.endpointListIsGrouped ? '' : columnName.QUEUELENGTH" v-model="activeColumn" v-tooltip title="Queue length: The number of messages waiting to be processed in the input queue(s) of the endpoint."
          >Queue Length<template #unit>(MSGS)</template>
        </SortableColumn>
      </div>
      <div role="columnheader" :aria-label="columnName.THROUGHPUT" class="table-col">
        <SortableColumn :sort-by="monitoringStore.endpointListIsGrouped ? '' : columnName.THROUGHPUT" v-model="activeColumn" v-tooltip title="Throughput: The number of messages per second successfully processed by a receiving endpoint."
          >Throughput<template #unit>(msgs/s)</template>
        </SortableColumn>
      </div>
      <div role="columnheader" :aria-label="columnName.SCHEDULEDRETRIES" class="table-col">
        <SortableColumn :sort-by="monitoringStore.endpointListIsGrouped ? '' : columnName.SCHEDULEDRETRIES" v-model="activeColumn" v-tooltip title="Scheduled retries: The number of messages per second scheduled for retries (immediate or delayed)."
          >Scheduled retries <template #unit>(msgs/s)</template>
        </SortableColumn>
      </div>
      <div role="columnheader" :aria-label="columnName.PROCESSINGTIME" class="table-col">
        <SortableColumn :sort-by="monitoringStore.endpointListIsGrouped ? '' : columnName.PROCESSINGTIME" v-model="activeColumn" v-tooltip title="Processing time: The time taken for a receiving endpoint to successfully process a message."
          >Processing Time <template #unit>(t)</template>
        </SortableColumn>
      </div>
      <div role="columnheader" :aria-label="columnName.CRITICALTIME" class="table-col">
        <SortableColumn
          :sort-by="monitoringStore.endpointListIsGrouped ? '' : columnName.CRITICALTIME"
          v-model="activeColumn"
          v-tooltip
          title="Critical time: The elapsed time from when a message was sent, until it was successfully processed by a receiving endpoint."
          >Critical Time <template #unit>(t)</template>
        </SortableColumn>
      </div>
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

.endpoint-group-title {
  font-size: 14px;
  font-weight: bold;
  margin: 20px 0 10px 15px;
}
</style>
