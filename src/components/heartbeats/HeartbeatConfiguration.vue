<script setup lang="ts">
import { sortOptions, useHeartbeatsStore } from "@/stores/HeartbeatsStore";
import { storeToRefs } from "pinia";
import { computed } from "vue";
import NoData from "../NoData.vue";
import { Endpoint } from "@/resources/Heartbeat";
import { getSortFunction } from "@/components/OrderBy.vue";
import { SortDirection } from "@/resources/SortOptions";
import TimeSince from "../TimeSince.vue";
import OnOffSwitch from "../OnOffSwitch.vue";

const store = useHeartbeatsStore();
const { endpoints, filterString, selectedSort } = storeToRefs(store);
const sortedEndpoints = computed<Endpoint[]>(() =>
  [...endpoints.value].filter((endpoint) => !filterString.value || endpoint.name.toLowerCase().includes(filterString.value.toLowerCase())).sort(selectedSort.value.sort ?? getSortFunction(sortOptions[0].selector, SortDirection.Ascending))
);
</script>

<template>
  <section name="endpoint_configuration">
    <div class="row">
      <div class="col-sm-12 no-side-padding">
        <div class="alert alert-warning">
          <i class="fa fa-warning" />
          <strong>Warning:</strong> The list of endpoints below only contains endpoints with the heartbeats plug-in installed. Toggling heartbeat monitoring won't toggle
          <a href="https://docs.particular.net/monitoring/metrics/in-servicepulse" target="_blank">performance monitoring</a>
          <i class="fa fa-external-link fake-link" />
        </div>

        <no-data v-if="endpoints.length === 0" message="Nothing to configure" />
        <template v-for="endpoint in sortedEndpoints" :key="endpoint.id">
          <div class="row box box-no-click" :class="{ 'box-info': endpoint.monitor_heartbeat, 'box-danger': !endpoint.monitor_heartbeat }">
            <div class="col-sm-12 no-side-padding">
              <div class="row">
                <div class="col-sm-2 col-lg-1">
                  <OnOffSwitch :id="endpoint.id" @toggle="store.toggleEndpointMonitor(endpoint)" v-model="endpoint.monitor_heartbeat" />
                </div>
                <div class="col-sm-10 col-lg-11">
                  <div class="row box-header">
                    <div class="col-xs-12">
                      <p class="lead">{{ endpoint.name }}<span class="de-emphasize">@</span>{{ endpoint.host_display_name }}</p>
                      <p class="endpoint-metadata" v-if="endpoint.heartbeat_information"><i class="fa fa-heartbeat"></i> <time-since :date-utc="endpoint.heartbeat_information?.last_report_at" /></p>
                      <p class="endpoint-metadata" v-if="!endpoint.heartbeat_information"><i class="fa fa-heartbeat"></i> No recent heartbeat information available</p>
                      <p class="endpoint-metadata" v-if="!endpoint.heartbeat_information"><i class="fa fa-plug"></i> No heartbeat plugin installed</p>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </template>
      </div>
    </div>
  </section>
</template>

<style scoped>
@import "../list.css";
</style>
