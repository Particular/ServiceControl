<script setup lang="ts">
import NoData from "../NoData.vue";
import { useHeartbeatsStore } from "@/stores/HeartbeatsStore";
import { storeToRefs } from "pinia";
import TimeSince from "../TimeSince.vue";

const store = useHeartbeatsStore();
const { activeEndpoints, filteredActiveEndpoints } = storeToRefs(store);
</script>

<template>
  <section name="active_endpoints">
    <no-data v-if="activeEndpoints.length === 0" message="No active endpoints"></no-data>
    <div v-if="activeEndpoints.length > 0" class="row">
      <div class="col-sm-12 no-side-padding">
        <div class="row box box-no-click" v-for="endpoint in filteredActiveEndpoints" :key="endpoint.id">
          <div class="col-sm-12 no-side-padding">
            <div class="row">
              <div class="col-sm-12 no-side-padding">
                <div class="row box-header">
                  <div class="col-sm-12 no-side-padding">
                    <p class="lead">
                      {{ store.endpointDisplayName(endpoint) }}
                    </p>
                    <p>latest heartbeat received <time-since :date-utc="endpoint.heartbeat_information?.last_report_at" /></p>
                    <p v-if="!endpoint.heartbeat_information">No plugin installed</p>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </section>
</template>

<style>
@import "../list.css";

p:not(.lead) {
  color: #777f7f;
  margin: 0 0 5px;
}
</style>
