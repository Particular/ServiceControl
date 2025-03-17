<script setup lang="ts">
import { RouterLink, RouterView } from "vue-router";
import routeLinks from "@/router/routeLinks";
import isRouteSelected from "@/composables/isRouteSelected";
import { useHeartbeatsStore } from "@/stores/HeartbeatsStore";
import { storeToRefs } from "pinia";
import ServiceControlAvailable from "@/components/ServiceControlAvailable.vue";
import LicenseNotExpired from "@/components/LicenseNotExpired.vue";
import FilterInput from "@/components/FilterInput.vue";
import { useIsMassTransitConnected } from "@/composables/useIsMassTransitConnected";

const store = useHeartbeatsStore();
const { unhealthyEndpoints, healthyEndpoints, endpointFilterString } = storeToRefs(store);

const isMassTransitConnected = useIsMassTransitConnected();
</script>

<template>
  <LicenseNotExpired>
    <ServiceControlAvailable>
      <div class="container">
        <div class="row">
          <div class="col-12">
            <h1>Endpoint Heartbeats</h1>
          </div>
          <div class="col-12" v-if="isMassTransitConnected">
            <div class="alert alert-info">MassTransit endpoints are currently not supported by heartbeat functionality and will not show in this view.</div>
          </div>
        </div>
        <div class="row">
          <div class="col-sm-12">
            <div class="tabs" role="tablist">
              <div>
                <!--Inactive Endpoints-->
                <h5 :class="{ active: isRouteSelected(routeLinks.heartbeats.unhealthy.link) }">
                  <RouterLink role="tab" :aria-selected="isRouteSelected(routeLinks.heartbeats.unhealthy.link)" :to="routeLinks.heartbeats.unhealthy.link"> Unhealthy Endpoints ({{ unhealthyEndpoints.length }}) </RouterLink>
                </h5>

                <!--Active Endpoints-->
                <h5 :class="{ active: isRouteSelected(routeLinks.heartbeats.healthy.link) }">
                  <RouterLink role="tab" :aria-selected="isRouteSelected(routeLinks.heartbeats.healthy.link)" :to="routeLinks.heartbeats.healthy.link"> Healthy Endpoints ({{ healthyEndpoints.length }}) </RouterLink>
                </h5>

                <!--Configuration-->
                <h5 :class="{ active: isRouteSelected(routeLinks.heartbeats.configuration.link) }">
                  <RouterLink role="tab" :aria-selected="isRouteSelected(routeLinks.heartbeats.configuration.link)" :to="routeLinks.heartbeats.configuration.link"> Configuration </RouterLink>
                </h5>
              </div>
              <div class="filter-group">
                <FilterInput v-model="endpointFilterString" />
              </div>
            </div>
          </div>
        </div>
        <RouterView />
      </div>
    </ServiceControlAvailable>
  </LicenseNotExpired>
</template>

<style scoped>
@import "@/assets/dropdown.css";

.tabs {
  display: flex;
  justify-content: space-between;
  align-items: end;
  flex-wrap: wrap;
}

.tabs .dropdown-menu li a {
  font-size: 14px;
}

.filter-group {
  display: flex;
  padding: 2px 0;
}

.msg-group-menu {
  margin: 0;
  padding: 0;
}

.form-control-static {
  min-height: 34px;
  padding-top: 7px;
  padding-bottom: 7px;
  margin-bottom: 0;
}

.filter-group > *:not(:first-child) {
  margin-left: 1.5em;
}
</style>
