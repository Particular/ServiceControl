<script setup lang="ts">
import ColumnHeader from "../ColumnHeader.vue";
import EndpointListRow from "./EndpointListRow.vue";
import { useMonitoringStore } from "@/stores/MonitoringStore";
import { EndpointName, Throughput, ScheduledRetries, ProcessingTime, CriticalTime, QueueLength } from "@/resources/MonitoringResources";
import { storeToRefs } from "pinia";

const monitoringStore = useMonitoringStore();
const { sortBy: activeColumn } = storeToRefs(monitoringStore);
</script>

<template>
  <section role="table" aria-label="endpoint-list">
    <!--Table headings-->
    <div role="row" aria-label="column-headers" class="table-head-row">
      <ColumnHeader :name="EndpointName.name" :label="EndpointName.label" class="table-first-col" v-model="activeColumn" sortable default-ascending />
      <ColumnHeader :name="QueueLength.name" :label="QueueLength.label" :unit="QueueLength.unit" class="table-col" v-model="activeColumn" sortable :sort-by="monitoringStore.endpointListIsGrouped ? '' : QueueLength.name">
        <template #help>{{ QueueLength.tooltip }}</template>
      </ColumnHeader>
      <ColumnHeader :name="Throughput.name" :label="Throughput.label" :unit="Throughput.unit" class="table-col" v-model="activeColumn" sortable :sort-by="monitoringStore.endpointListIsGrouped ? '' : Throughput.name">
        <template #help>{{ Throughput.tooltip }}</template>
      </ColumnHeader>
      <ColumnHeader :name="ScheduledRetries.name" :label="ScheduledRetries.label" :unit="ScheduledRetries.unit" class="table-col" v-model="activeColumn" sortable :sort-by="monitoringStore.endpointListIsGrouped ? '' : ScheduledRetries.name">
        <template #help>{{ ScheduledRetries.tooltip }}</template>
      </ColumnHeader>
      <ColumnHeader :name="ProcessingTime.name" :label="ProcessingTime.label" :unit="ProcessingTime.unit" class="table-col" v-model="activeColumn" sortable :sort-by="monitoringStore.endpointListIsGrouped ? '' : ProcessingTime.name">
        <template #help>{{ ProcessingTime.unit }}</template>
      </ColumnHeader>
      <ColumnHeader :name="CriticalTime.name" :label="CriticalTime.label" :unit="CriticalTime.unit" class="table-col" v-model="activeColumn" sortable :sort-by="monitoringStore.endpointListIsGrouped ? '' : CriticalTime.name">
        <template #help>{{ CriticalTime.tooltip }}</template>
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
