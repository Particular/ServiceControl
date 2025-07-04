<script setup lang="ts">
import { useHeartbeatsStore, ColumnNames } from "@/stores/HeartbeatsStore";
import { storeToRefs } from "pinia";
import DataView from "@/components/DataView.vue";
import OnOffSwitch from "../OnOffSwitch.vue";
import routeLinks from "@/router/routeLinks";
import { useRoute } from "vue-router";
import { Tippy } from "vue-tippy";
import { LogicalEndpoint } from "@/resources/Heartbeat";
import { useShowToast } from "@/composables/toast";
import { TYPE } from "vue-toastification";
import LastHeartbeat from "@/components/heartbeats/LastHeartbeat.vue";
import ColumnHeader from "../ColumnHeader.vue";
import FAIcon from "@/components/FAIcon.vue";
import { faBellSlash, faCheck, faCloud, faServer } from "@fortawesome/free-solid-svg-icons";

defineProps<{
  data: LogicalEndpoint[];
  columns: ColumnNames[];
}>();

const store = useHeartbeatsStore();
const { sortByInstances, itemsPerPage } = storeToRefs(store);
const route = useRoute();

async function changeEndpointSettings(endpoint: LogicalEndpoint) {
  try {
    await store.updateEndpointSettings([endpoint]);
    useShowToast(TYPE.SUCCESS, "Saved", "", false, { timeout: 3000 });
  } catch {
    useShowToast(TYPE.ERROR, "Save failed", "", false, { timeout: 3000 });
  }
}

function endpointHealth(endpoint: LogicalEndpoint) {
  if (endpoint.alive_count === 0) return "text-danger";
  if (endpoint.track_instances) {
    return endpoint.down_count > 0 ? "text-warning" : "text-success";
  } else return "text-success";
}
</script>

<template>
  <section role="table" aria-label="endpoint-instances">
    <!--Table headings-->
    <div role="row" aria-label="column-headers" class="row table-head-row" :style="{ borderTop: 0 }">
      <ColumnHeader v-if="columns.includes(ColumnNames.Name)" :name="ColumnNames.Name" label="Name" class="col-6" sortable v-model="sortByInstances" default-ascending />
      <ColumnHeader v-if="columns.includes(ColumnNames.InstancesDown)" :name="ColumnNames.InstancesDown" label="Instances Down" class="col-2" sortable v-model="sortByInstances" default-ascending />
      <ColumnHeader v-if="columns.includes(ColumnNames.InstancesTotal)" :name="ColumnNames.InstancesTotal" label="Instances" class="col-2" sortable v-model="sortByInstances" default-ascending />
      <ColumnHeader v-if="columns.includes(ColumnNames.LastHeartbeat)" :name="ColumnNames.LastHeartbeat" label="Last Heartbeat" class="col-2" sortable v-model="sortByInstances" />
      <ColumnHeader v-if="columns.includes(ColumnNames.Tracked)" :name="ColumnNames.Tracked" label="Track Instances" class="col-1 centre" sortable v-model="sortByInstances" />
      <ColumnHeader v-if="columns.includes(ColumnNames.TrackToggle)" :name="ColumnNames.TrackToggle" label="Track Instances" class="col-2 centre" sortable v-model="sortByInstances" />
      <ColumnHeader v-if="columns.includes(ColumnNames.Muted)" :name="ColumnNames.Muted" label="Instances Muted" class="col-1 centre" sortable v-model="sortByInstances" />
    </div>
    <!--Table rows-->
    <DataView :data="data" :show-items-per-page="true" :items-per-page="itemsPerPage" @items-per-page-changed="store.setItemsPerPage">
      <template #data="{ pageData }">
        <div role="rowgroup" aria-label="endpoints">
          <div role="row" :aria-label="endpoint.name" class="row grid-row" v-for="endpoint in pageData" :key="endpoint.name">
            <div v-if="columns.includes(ColumnNames.Name)" role="cell" aria-label="instance-name" class="col-6 host-name">
              <div class="box-header">
                <tippy :aria-label="endpoint.name" :delay="[700, 0]" class="no-side-padding lead righ-side-ellipsis endpoint-details-link">
                  <template #content>
                    <p :style="{ overflowWrap: 'break-word' }">{{ endpoint.name }}</p>
                  </template>
                  <RouterLink class="hackToPreventSafariFromShowingTooltip" aria-label="details-link" :to="{ path: routeLinks.heartbeats.instances.link(endpoint.name), query: { back: route.path } }">
                    {{ endpoint.name }}
                  </RouterLink>
                </tippy>
              </div>
            </div>
            <div v-if="columns.includes(ColumnNames.InstancesTotal) || columns.includes(ColumnNames.InstancesDown)" role="cell" class="col-2">
              <tippy :delay="[300, 0]">
                <template #content>
                  <template v-if="endpoint.track_instances">
                    <p>Tracking all instances</p>
                    <p>{{ endpoint.alive_count }} alive</p>
                    <p>{{ endpoint.down_count }} no heartbeat</p>
                  </template>
                  <template v-else>
                    <p>Not tracking instances</p>
                    <p>{{ endpoint.alive_count }} alive</p>
                  </template>
                </template>
                <FAIcon :icon="endpoint.track_instances ? faServer : faCloud" class="icon" :class="endpointHealth(endpoint)" />
                <span class="endpoint-count" aria-label="instance-count">{{ store.instanceDisplayText(endpoint) }}</span>
              </tippy>
            </div>
            <div v-if="columns.includes(ColumnNames.LastHeartbeat)" role="cell" aria-label="last-heartbeat" class="col-2 last-heartbeat">
              <LastHeartbeat :date="endpoint.heartbeat_information?.last_report_at" tooltip-target="endpoint" />
            </div>
            <div v-if="columns.includes(ColumnNames.Tracked)" role="cell" aria-label="tracked-instances" class="col-1 centre">
              <tippy v-if="endpoint.track_instances" id="tracked-instance-desc" content="Instances are being tracked" :delay="[1000, 0]">
                <FAIcon :icon="faCheck" class="text-success" aria-title="Instances are being tracked" />
              </tippy>
            </div>
            <div v-if="columns.includes(ColumnNames.TrackToggle)" role="cell" aria-label="tracked-instances" class="col-2 centre">
              <div class="switch">
                <OnOffSwitch :id="endpoint.name" @toggle="changeEndpointSettings(endpoint)" :value="endpoint.track_instances" />
              </div>
            </div>
            <div v-if="columns.includes(ColumnNames.Muted)" role="cell" aria-label="muted" class="col-1 centre">
              <template v-if="endpoint.muted_count === endpoint.alive_count + endpoint.down_count">
                <tippy content="All instances have alerts muted" :delay="[300, 0]">
                  <FAIcon :icon="faBellSlash" class="text-danger" />
                </tippy>
                <span class="instances-muted" aria-label="Muted instance count">{{ endpoint.muted_count }}</span>
              </template>
              <template v-else-if="endpoint.muted_count > 0">
                <tippy :content="`${endpoint.muted_count} instance(s) have alerts muted`" :delay="[300, 0]">
                  <FAIcon :icon="faBellSlash" class="text-warning" />
                </tippy>
                <span class="instances-muted">{{ endpoint.muted_count }}</span>
              </template>
            </div>
          </div>
        </div>
      </template>
    </DataView>
  </section>
</template>

<style scoped>
@import "../list.css";
@import "./heartbeats.css";

.hackToPreventSafariFromShowingTooltip::after {
  content: "";
  display: block;
}

.instances-muted {
  font-weight: bold;
}

.icon {
  padding-right: 6px;
}
</style>
