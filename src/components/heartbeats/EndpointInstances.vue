<script setup lang="ts">
import NoData from "../NoData.vue";
import { storeToRefs } from "pinia";
import { useRoute, useRouter } from "vue-router";
import { computed, onMounted, ref } from "vue";
import { EndpointStatus } from "@/resources/Heartbeat";
import ColumnHeader from "@/components/ColumnHeader.vue";
import DataView from "@/components/DataView.vue";
import OnOffSwitch from "../OnOffSwitch.vue";
import routeLinks from "@/router/routeLinks";
import { useShowToast } from "@/composables/toast";
import { TYPE } from "vue-toastification";
import { ColumnNames } from "@/stores/HeartbeatInstancesStore";
import { EndpointsView } from "@/resources/EndpointView";
import endpointSettingsClient from "@/components/heartbeats/endpointSettingsClient";
import { EndpointSettings } from "@/resources/EndpointSettings";
import ConfirmDialog from "@/components/ConfirmDialog.vue";
import LastHeartbeat from "@/components/heartbeats/LastHeartbeat.vue";
import FilterInput from "../FilterInput.vue";
import ResultsCount from "../ResultsCount.vue";
import { faBell, faBellSlash, faChevronLeft, faHeartbeat, faTrash } from "@fortawesome/free-solid-svg-icons";
import FAIcon from "@/components/FAIcon.vue";
import useHeartbeatInstancesStoreAutoRefresh from "@/composables/useHeartbeatInstancesStoreAutoRefresh";

enum Operation {
  Mute = "mute",
  Unmute = "unmute",
}

const route = useRoute();
const router = useRouter();
const endpointName = route.params.endpointName.toString();
const { store } = useHeartbeatInstancesStoreAutoRefresh();
const { filteredInstances, sortedInstances, instanceFilterString, sortByInstances } = storeToRefs(store);
const endpointSettings = ref<EndpointSettings[]>([endpointSettingsClient.defaultEndpointSettingsValue()]);
const backLink = ref<string>(routeLinks.heartbeats.root);
const filterToValidInstances = (data: EndpointsView[]) =>
  data
    .filter((instance) => instance.name === endpointName)
    .filter((instance) => {
      const trackInstances = (endpointSettings.value.find((value) => value.name === instance.name) ?? endpointSettings.value.find((value) => value.name === ""))!.track_instances;
      if (!trackInstances && !instance.is_sending_heartbeats) {
        return false;
      }

      return true;
    });
const filteredValidInstances = computed(() => filterToValidInstances(filteredInstances.value));
const totalValidInstances = computed(() => filterToValidInstances(sortedInstances.value));
const showBulkWarningDialog = ref(false);
const dialogWarningOperation = ref(Operation.Mute);

onMounted(async () => {
  const back = useRouter().currentRoute.value.query.back as string;
  if (back) {
    backLink.value = back;
  }
  endpointSettings.value = await endpointSettingsClient.endpointSettings();
});

function showBulkOperationWarningDialog(operation: Operation) {
  dialogWarningOperation.value = operation;
  showBulkWarningDialog.value = true;
}

function cancelWarningDialog() {
  showBulkWarningDialog.value = false;
}

async function proceedWarningDialog() {
  showBulkWarningDialog.value = false;

  try {
    await store.toggleEndpointMonitor(
      filteredValidInstances.value.filter((instance) => (dialogWarningOperation.value === Operation.Unmute && !instance.monitor_heartbeat) || (dialogWarningOperation.value === Operation.Mute && instance.monitor_heartbeat))
    );
    useShowToast(TYPE.SUCCESS, `All endpoint instances ${dialogWarningOperation.value}`, "", false, { timeout: 1000 });
  } catch {
    useShowToast(TYPE.ERROR, "Save failed", "", false, { timeout: 3000 });
  }
}

async function deleteInstance(instance: EndpointsView) {
  try {
    await store.deleteEndpointInstance(instance);
    useShowToast(TYPE.SUCCESS, "Endpoint instance deleted", "", false, { timeout: 1000 });
  } catch {
    useShowToast(TYPE.ERROR, "Delete failed", "", false, { timeout: 3000 });
  }
}

async function deleteAllInstances() {
  try {
    await Promise.all(sortedInstances.value.filter((instance) => instance.name === endpointName).map((instance) => store.deleteEndpointInstance(instance)));
    useShowToast(TYPE.SUCCESS, "Endpoint deleted", "", false, { timeout: 1000 });
    await router.replace(backLink.value);
  } catch {
    useShowToast(TYPE.ERROR, "Delete failed", "", false, { timeout: 3000 });
  }
}

async function toggleAlerts(instance: EndpointsView) {
  try {
    await store.toggleEndpointMonitor([instance]);
    useShowToast(TYPE.SUCCESS, `Endpoint instance ${!instance.monitor_heartbeat ? "muted" : "unmuted"}`, "", false, { timeout: 1000 });
  } catch {
    useShowToast(TYPE.ERROR, "Save failed", "", false, { timeout: 3000 });
  }
}
</script>

<template>
  <Teleport to="#modalDisplay">
    <ConfirmDialog
      v-if="showBulkWarningDialog"
      heading="Proceed with bulk operation"
      :body="`Are you sure you want to ${dialogWarningOperation} ${filteredValidInstances.length} endpoint instance(s)?`"
      @cancel="cancelWarningDialog"
      @confirm="proceedWarningDialog"
    />
  </Teleport>
  <div class="container">
    <div class="row">
      <div class="col-8 instances-heading">
        <RouterLink :to="backLink"><FAIcon :icon="faChevronLeft" size="sm" /> Back</RouterLink>
        <h1 :style="{ overflowWrap: 'break-word' }">{{ endpointName }} Instances</h1>
      </div>
      <div class="col-4 align-content-center">
        <div class="searchContainer">
          <FilterInput v-model="instanceFilterString" />
        </div>
      </div>
    </div>
    <div class="row filters">
      <div class="col-sm-12">
        <span class="buttonsContainer">
          <button type="button" class="btn btn-warning btn-sm" :disabled="filteredValidInstances.length === 0" @click="showBulkOperationWarningDialog(Operation.Mute)">
            <FAIcon :icon="faBellSlash" class="icon" :class="{ 'text-black': filteredValidInstances.length > 0 }" />
            Mute Alerts on All
          </button>
          <button type="button" class="btn btn-default btn-sm" :disabled="filteredValidInstances.length === 0" @click="showBulkOperationWarningDialog(Operation.Unmute)">
            <FAIcon :icon="faBell" class="icon" :class="{ 'text-black': filteredValidInstances.length > 0 }" />
            Unmute Alerts on All
          </button>
        </span>
      </div>
    </div>
    <div class="row">
      <ResultsCount :displayed="filteredValidInstances.length" :total="totalValidInstances.length" />
    </div>
    <section role="table" aria-label="endpoint-instances">
      <!--Table headings-->
      <div role="row" aria-label="column-headers" class="row table-head-row" :style="{ borderTop: 0 }">
        <ColumnHeader :name="ColumnNames.InstanceName" label="Host Name" class="col-6" v-model="sortByInstances" sortable default-ascending />
        <ColumnHeader :name="ColumnNames.LastHeartbeat" label="Last Heartbeat" class="col-2" v-model="sortByInstances" sortable />
        <ColumnHeader :name="ColumnNames.MuteToggle" label="Mute Alerts" class="col-2 centre">
          <template #help>Mute an instance when you are planning to take the instance offline to do maintenance or some other reason. This will prevent alerts on the dashboard.</template>
        </ColumnHeader>
        <ColumnHeader name="actions" label="Actions" class="col-1">
          <template #help>
            <div class="d-flex align-items-center p-1">
              <button type="button" class="btn btn-danger btn-ms text-nowrap me-3"><FAIcon :icon="faTrash" class="icon text-white" /> Delete</button>
              <span>Delete an instance when that instance has been decommissioned.</span>
            </div>
          </template>
        </ColumnHeader>
      </div>
      <no-data v-if="filteredValidInstances.length === 0" message="No endpoint instances found. For untracked endpoints, disconnected instances are automatically pruned.">
        <div class="delete-all">
          <span>You may</span>
          <button type="button" @click="deleteAllInstances()" class="btn btn-danger btn-sm"><FAIcon :icon="faTrash" class="icon text-white" /> Delete</button>
          <span>this endpoint</span>
        </div>
      </no-data>
      <!--Table rows-->
      <DataView :data="filteredValidInstances" :show-items-per-page="true" :items-per-page="20">
        <template #data="{ pageData }">
          <div role="rowgroup" aria-label="endpoints">
            <div role="row" :aria-label="instance.name" class="row grid-row" v-for="instance in pageData" :key="instance.id">
              <div role="cell" class="col-6 host-name">
                <span role="status" class="status-icon">
                  <FAIcon v-if="instance.heartbeat_information?.reported_status !== EndpointStatus.Alive" aria-label="instance dead" :icon="faHeartbeat" class="text-danger" />
                  <FAIcon v-else aria-label="instance alive" :icon="faHeartbeat" class="text-success" />
                </span>
                <span class="lead" aria-label="instance-name">{{ instance.host_display_name }}</span>
              </div>
              <div role="cell" aria-label="last-heartbeat" class="col-2 last-heartbeat">
                <LastHeartbeat :date="instance.heartbeat_information?.last_report_at" tooltip-target="instance" />
              </div>
              <div role="cell" aria-label="mute toggle" class="col-2 centre">
                <div class="switch">
                  <OnOffSwitch :id="instance.host_display_name" @toggle="toggleAlerts(instance)" :value="!instance.monitor_heartbeat" />
                </div>
              </div>
              <div role="cell" aria-label="actions" class="col-1 actions">
                <button v-if="instance.heartbeat_information?.reported_status !== EndpointStatus.Alive" type="button" @click="deleteInstance(instance)" class="btn btn-danger btn-sm"><FAIcon :icon="faTrash" class="icon text-white" /> Delete</button>
              </div>
            </div>
          </div>
        </template>
      </DataView>
    </section>
  </div>
</template>

<style scoped>
@import "../list.css";
@import "./heartbeats.css";

.searchContainer {
  display: flex;
  justify-content: flex-end;
}

.instances-heading h1 {
  margin-bottom: 10px;
}

.status-icon {
  width: 16px;
  margin-right: 4px;
}

.actions {
  display: flex;
}

.filters {
  margin-top: 0.25em;
  margin-bottom: 0.25em;
}

.buttonsContainer {
  background-color: #f3f3f3;
  display: flex;
  gap: 0.5em;
  border: #8c8c8c 1px solid;
  border-radius: 3px;
  padding: 0.4em;
}

.delete-all {
  display: flex;
  align-items: center;
  gap: 0.4em;
}

.icon {
  color: var(--reduced-emphasis);
}
</style>
