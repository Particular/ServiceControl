<script setup lang="ts">
import NoData from "../NoData.vue";
import { ColumnNames } from "@/stores/HeartbeatsStore";
import { storeToRefs } from "pinia";
import HeartbeatsList from "./HeartbeatsList.vue";
import ResultsCount from "../ResultsCount.vue";
import useHeartbeatsStoreAutoRefresh from "@/composables/useHeartbeatsStoreAutoRefresh";

const { store } = useHeartbeatsStoreAutoRefresh();
const { healthyEndpoints, filteredHealthyEndpoints } = storeToRefs(store);
</script>

<template>
  <div class="row">
    <ResultsCount :displayed="filteredHealthyEndpoints.length" :total="healthyEndpoints.length" />
  </div>
  <section name="healthy_endpoints" aria-label="Healthy Endpoints">
    <no-data v-if="healthyEndpoints.length === 0" message="No healthy endpoints"></no-data>
    <div v-if="healthyEndpoints.length > 0" class="row">
      <div class="col-sm-12 no-side-padding">
        <HeartbeatsList :data="filteredHealthyEndpoints" :columns="[ColumnNames.Name, ColumnNames.InstancesTotal, ColumnNames.LastHeartbeat, ColumnNames.Tracked, ColumnNames.Muted]" />
      </div>
    </div>
  </section>
</template>
