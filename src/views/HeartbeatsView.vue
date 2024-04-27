<script setup lang="ts">
import { RouterLink, RouterView } from "vue-router";
import { licenseStatus } from "../composables/serviceLicense";
import { connectionState } from "../composables/serviceServiceControl";
import LicenseExpired from "../components/LicenseExpired.vue";
import routeLinks from "@/router/routeLinks";
import isRouteSelected from "@/composables/isRouteSelected";
import { DisplayType, sortOptions, useHeartbeatsStore } from "@/stores/HeartbeatsStore";
import { storeToRefs } from "pinia";
import OrderBy from "@/components/OrderBy.vue";

const store = useHeartbeatsStore();
const { inactiveEndpoints, activeEndpoints, selectedDisplay, filterString } = storeToRefs(store);
</script>

<template>
  <LicenseExpired />
  <template v-if="!licenseStatus.isExpired">
    <div class="container">
      <div class="row">
        <div class="col-12">
          <h1>Endpoint Heartbeats</h1>
        </div>
      </div>
      <div class="row">
        <div class="col-sm-12">
          <div class="tabs">
            <div>
              <!--Inactive Endpoints-->
              <h5 :class="{ active: isRouteSelected(routeLinks.heartbeats.inactive.link), disabled: !connectionState.connected && !connectionState.connectedRecently }">
                <RouterLink :to="routeLinks.heartbeats.inactive.link"> Inactive Endpoints ({{ inactiveEndpoints.length }}) </RouterLink>
              </h5>

              <!--Active Endpoints-->
              <h5 v-if="!licenseStatus.isExpired" :class="{ active: isRouteSelected(routeLinks.heartbeats.active.link), disabled: !connectionState.connected && !connectionState.connectedRecently }">
                <RouterLink :to="routeLinks.heartbeats.active.link"> Active Endpoints ({{ activeEndpoints.length }}) </RouterLink>
              </h5>

              <!--Configuration-->
              <h5 v-if="!licenseStatus.isExpired" :class="{ active: isRouteSelected(routeLinks.heartbeats.configuration.link), disabled: !connectionState.connected && !connectionState.connectedRecently }">
                <RouterLink :to="routeLinks.heartbeats.configuration.link"> Configuration </RouterLink>
              </h5>
            </div>
            <div class="filter-group">
              <div class="msg-group-menu dropdown" v-if="!isRouteSelected(routeLinks.heartbeats.configuration.link)">
                <label class="control-label">Display:</label>
                <button type="button" class="btn btn-default dropdown-toggle sp-btn-menu" data-bs-toggle="dropdown" aria-haspopup="true" aria-expanded="false">
                  {{ selectedDisplay }}
                  <span class="caret"></span>
                </button>
                <ul class="dropdown-menu">
                  <li v-for="displayType in DisplayType" :key="displayType">
                    <a @click.prevent="store.setSelectedDisplay(displayType)">{{ displayType }}</a>
                  </li>
                </ul>
              </div>
              <OrderBy @sort-updated="store.setSelectedSort" :sort-options="sortOptions" />
              <div class="filter-input">
                <input type="text" placeholder="Filter by name..." aria-label="filter by name" class="form-control-static filter-input" v-model="filterString" />
              </div>
            </div>
          </div>
        </div>
      </div>
      <RouterView />
    </div>
  </template>
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

.filter-input input {
  display: inline-block;
  width: 100%;
  padding-right: 10px;
  padding-left: 30px;
  border: 1px solid #aaa;
  border-radius: 4px;
}

div.filter-input {
  position: relative;
  width: 280px;
}

.filter-input:before {
  font-family: "FontAwesome";
  width: 1.43em;
  content: "\f0b0";
  color: #919e9e;
  position: absolute;
  top: calc(50% - 0.7em);
  left: 0.75em;
}
</style>
