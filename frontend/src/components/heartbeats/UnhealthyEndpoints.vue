<script setup lang="ts">
import NoData from "../NoData.vue";
import { ColumnNames } from "@/stores/HeartbeatsStore";
import { storeToRefs } from "pinia";
import HeartbeatsList from "./HeartbeatsList.vue";
import ResultsCount from "../ResultsCount.vue";
import useHeartbeatsStoreAutoRefresh from "@/composables/useHeartbeatsStoreAutoRefresh";

const { store } = useHeartbeatsStoreAutoRefresh();
const { unhealthyEndpoints, filteredUnhealthyEndpoints } = storeToRefs(store);
</script>

<template>
  <div class="row">
    <ResultsCount :displayed="filteredUnhealthyEndpoints.length" :total="unhealthyEndpoints.length" />
  </div>
  <section name="unhealthy_endpoints" aria-label="Unhealthy Endpoints">
    <no-data v-if="unhealthyEndpoints.length === 0" message="No unhealthy endpoints"></no-data>
    <div v-else class="row">
      <div class="col-sm-12 no-side-padding">
        <HeartbeatsList :data="filteredUnhealthyEndpoints" :columns="[ColumnNames.Name, ColumnNames.InstancesDown, ColumnNames.LastHeartbeat, ColumnNames.Tracked, ColumnNames.Muted]" />
      </div>
    </div>
  </section>
</template>
