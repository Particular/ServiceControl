<script setup lang="ts">
import NoData from "../NoData.vue";
import { DisplayType, useHeartbeatsStore } from "@/stores/HeartbeatsStore";
import { storeToRefs } from "pinia";
import TimeSince from "../TimeSince.vue";

const store = useHeartbeatsStore();
const { inactiveEndpoints, filteredInactiveEndpoints, selectedDisplay } = storeToRefs(store);
</script>

<template>
  <section name="inactive_endpoints">
    <no-data v-if="inactiveEndpoints.length === 0" message="No inactive endpoints"></no-data>
    <div v-if="inactiveEndpoints.length > 0" class="row">
      <div class="col-sm-12 no-side-padding">
        <div class="row box box-no-click" v-for="endpoint in filteredInactiveEndpoints" :key="endpoint.id">
          <div class="col-sm-12 no-side-padding">
            <div class="row">
              <div class="col-sm-12 no-side-padding">
                <div class="row box-header">
                  <div class="col-sm-12 no-side-padding">
                    <p class="lead">
                      {{ store.endpointDisplayName(endpoint) }}
                      <a class="remove-item" v-if="selectedDisplay === DisplayType.Instances" @click="store.deleteEndpoint(endpoint)">
                        <i class="fa fa-trash" v-tooltip :title="`Remove endpoint from list`" />
                      </a>
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

a.remove-item {
  margin-left: 5px;
  outline: none;
  border: none;
}

a.remove-item .fa {
  color: #00a3c4;
}
</style>
