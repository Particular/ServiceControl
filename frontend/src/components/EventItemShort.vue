<script setup lang="ts">
import DataView from "@/components/AutoRefreshDataView.vue";
import EventLogItem from "@/components/EventLogItem.vue";
import type EventLogItemType from "@/resources/EventLogItem";
import { ref } from "vue";
import { RouterLink } from "vue-router";

import type DataViewPageModel from "@/components/DataViewPageModel";
import routeLinks from "@/router/routeLinks";

const pageModel = ref<DataViewPageModel<EventLogItemType>>({ data: [], totalCount: 0 });
</script>

<template>
  <div class="events">
    <DataView api-url="eventlogitems" v-model="pageModel" :auto-refresh-seconds="5" :itemsPerPage="10" :show-pagination="false">
      <template #data>
        <div class="col-12">
          <h6>Last 10 events</h6>
          <EventLogItem v-for="item of pageModel.data" :eventLogItem="item" :key="item.id" />
        </div>
      </template>
      <template #footer>
        <div v-if="pageModel.totalCount > 10" class="row text-center">
          <div class="col-12">
            <RouterLink :to="routeLinks.events" class="btn btn-default btn-secondary btn-all-events">View all events</RouterLink>
          </div>
        </div>
      </template>
    </DataView>
  </div>
</template>

<style scoped>
.events {
  margin-top: 2em;
}

.btn.btn-all-events {
  width: 12em;
  margin-top: 2em;
}
</style>
