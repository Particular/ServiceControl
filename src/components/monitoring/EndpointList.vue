<script setup lang="ts">
import ColumnHeader from "../ColumnHeader.vue";
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
      <ColumnHeader :name="columnName.ENDPOINTNAME" label="Endpoint name" column-class="table-first-col" v-model="activeColumn" sortable default-ascending />
      <ColumnHeader :name="columnName.QUEUELENGTH" label="Queue length" unit="(msgs)" column-class="table-col" v-model="activeColumn" sortable :sort-by="monitoringStore.endpointListIsGrouped ? '' : columnName.QUEUELENGTH">
        <template #help>Queue length: The number of messages waiting to be processed in the input queue(s) of the endpoint.</template>
      </ColumnHeader>
      <ColumnHeader :name="columnName.THROUGHPUT" label="Throughput" unit="(msgs/s)" column-class="table-col" v-model="activeColumn" sortable :sort-by="monitoringStore.endpointListIsGrouped ? '' : columnName.THROUGHPUT">
        <template #help>Throughput: The number of messages per second successfully processed by a receiving endpoint.</template>
      </ColumnHeader>
      <ColumnHeader :name="columnName.SCHEDULEDRETRIES" label="Scheduled retries" unit="(msgs/s)" column-class="table-col" v-model="activeColumn" sortable :sort-by="monitoringStore.endpointListIsGrouped ? '' : columnName.SCHEDULEDRETRIES">
        <template #help>Scheduled retries: The number of messages per second scheduled for retries (immediate or delayed).</template>
      </ColumnHeader>
      <ColumnHeader :name="columnName.PROCESSINGTIME" label="Processing time" unit="(t)" column-class="table-col" v-model="activeColumn" sortable :sort-by="monitoringStore.endpointListIsGrouped ? '' : columnName.PROCESSINGTIME">
        <template #help>Processing time: The time taken for a receiving endpoint to successfully process a message.</template>
      </ColumnHeader>
      <ColumnHeader :name="columnName.CRITICALTIME" label="Critical time" unit="(t)" column-class="table-col" v-model="activeColumn" sortable :sort-by="monitoringStore.endpointListIsGrouped ? '' : columnName.CRITICALTIME">
        <template #help>Critical time: The elapsed time from when a message was sent, until it was successfully processed by a receiving endpoint.</template>
      </ColumnHeader>
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
