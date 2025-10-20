<script setup lang="ts">
import { licenseStatus } from "@/composables/serviceLicense";
import LicenseExpired from "@/components/LicenseExpired.vue";
import DataView from "@/components/AutoRefreshDataView.vue";
import EventLogItem from "@/components/EventLogItem.vue";
import ServiceControlNotAvailable from "@/components/ServiceControlNotAvailable.vue";
import type EventLogItemType from "@/resources/EventLogItem";
import { ref } from "vue";
import type DataViewPageModel from "@/components/DataViewPageModel";
import useConnectionsAndStatsAutoRefresh from "@/composables/useConnectionsAndStatsAutoRefresh";

const pageModel = ref<DataViewPageModel<EventLogItemType>>({ data: [], totalCount: 0 });
const { store: connectionStore } = useConnectionsAndStatsAutoRefresh();
const connectionState = connectionStore.connectionState;
</script>

<template>
  <LicenseExpired />
  <template v-if="!licenseStatus.isExpired">
    <ServiceControlNotAvailable />
    <template v-if="connectionState.connected">
      <div class="container">
        <div class="row">
          <div class="col-12">
            <h1>Events</h1>
          </div>
        </div>

        <div class="row">
          <div class="col-12">
            <div class="events-view">
              <DataView api-url="eventlogitems" v-model="pageModel" :auto-refresh-seconds="5" :show-items-per-page="true" :items-per-page="20">
                <template #data>
                  <EventLogItem v-for="item of pageModel.data" :eventLogItem="item" :key="item.id" />
                </template>
              </DataView>
            </div>
          </div>
        </div>
      </div>
    </template>
  </template>
</template>
