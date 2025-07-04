<script setup lang="ts">
import { ColumnNames, useHeartbeatsStore } from "@/stores/HeartbeatsStore";
import { storeToRefs } from "pinia";
import NoData from "../NoData.vue";
import { useShowToast } from "@/composables/toast";
import { TYPE } from "vue-toastification";
import EndpointSettingsSupported from "@/components/heartbeats/EndpointSettingsSupported.vue";
import OnOffSwitch from "../OnOffSwitch.vue";
import HeartbeatsList from "./HeartbeatsList.vue";
import { ref } from "vue";
import ConfirmDialog from "@/components/ConfirmDialog.vue";
import ResultsCount from "../ResultsCount.vue";
import FAIcon from "@/components/FAIcon.vue";
import { faCloud, faServer } from "@fortawesome/free-solid-svg-icons";

enum Operation {
  Track = "track",
  DoNotTrack = "do not track",
}

const store = useHeartbeatsStore();
const { sortedEndpoints, filteredEndpoints, defaultTrackingInstancesValue } = storeToRefs(store);
const showBulkWarningDialog = ref(false);
const dialogWarningOperation = ref(Operation.Track);

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
    await store.updateEndpointSettings(
      filteredEndpoints.value.filter((endpoint) => (dialogWarningOperation.value === Operation.Track && !endpoint.track_instances) || (dialogWarningOperation.value === Operation.DoNotTrack && endpoint.track_instances))
    );
    useShowToast(TYPE.SUCCESS, `All endpoints set to '${dialogWarningOperation.value}'`, "", false, { timeout: 1000 });
  } catch {
    useShowToast(TYPE.ERROR, "Save failed", "", false, { timeout: 3000 });
  }
}

async function toggleDefaultSetting() {
  try {
    await store.updateEndpointSettings([{ name: "", track_instances: defaultTrackingInstancesValue.value }]);
    useShowToast(TYPE.SUCCESS, "Default setting updated", "", false, { timeout: 3000 });
  } catch {
    useShowToast(TYPE.ERROR, "Failed to update default setting", "", false, { timeout: 3000 });
  }
}
</script>

<template>
  <EndpointSettingsSupported>
    <Teleport to="#modalDisplay">
      <ConfirmDialog
        v-if="showBulkWarningDialog"
        heading="Proceed with bulk operation"
        :body="`Are you sure you want to set ${filteredEndpoints.length} endpoint(s) to be '${dialogWarningOperation}'?`"
        @cancel="cancelWarningDialog"
        @confirm="proceedWarningDialog"
      />
    </Teleport>
    <div class="row filters">
      <div class="col-sm-12">
        <span class="buttonsContainer">
          <button type="button" class="btn btn-default btn-sm" :disabled="filteredEndpoints.length === 0" @click="showBulkOperationWarningDialog(Operation.Track)">
            <FAIcon :icon="faServer" class="icon" :class="{ 'text-black': filteredEndpoints.length > 0 }" />
            Track Instances on All Endpoints
          </button>
          <button type="button" class="btn btn-default btn-sm" :disabled="filteredEndpoints.length === 0" @click="showBulkOperationWarningDialog(Operation.DoNotTrack)">
            <FAIcon :icon="faCloud" class="icon" :class="{ 'text-black': filteredEndpoints.length > 0 }" />
            Do Not Track Instances on All Endpoints
          </button>
        </span>
      </div>
    </div>
    <div class="row">
      <ResultsCount :displayed="filteredEndpoints.length" :total="sortedEndpoints.length" />
    </div>
    <section name="endpoint_configuration" aria-label="Endpoint Configuration">
      <div class="row">
        <div class="col-9 no-side-padding">
          <no-data v-if="sortedEndpoints.length === 0" message="Nothing to configure" />
          <div v-else class="row no-side-padding">
            <HeartbeatsList :data="filteredEndpoints" :columns="[ColumnNames.Name, ColumnNames.InstancesTotal, ColumnNames.LastHeartbeat, ColumnNames.TrackToggle]" />
          </div>
        </div>
        <div class="col-3 instructions">
          <div class="defaultSetting">
            <label>Track Instances by default on new endpoints</label>
            <div class="switch">
              <OnOffSwitch id="defaultTIV" @toggle="toggleDefaultSetting" :value="defaultTrackingInstancesValue" />
            </div>
          </div>
          <p>
            <template v-if="defaultTrackingInstancesValue">If most of your endpoints are auto-scaled, consider changing this setting.</template>
            <template v-else>If most of your endpoint are hosted in physical infrastructure, consider changing this setting.</template>
          </p>
          <p><code>Track Instances</code> is the best setting for endpoints where all instances are hosted in physical infrastructure that is not auto-scaled. Example, physical or virtual servers.</p>
          <p><code>Do Not Track Instances</code> is the best setting for endpoints that are hosted in infrastructure with autoscalers. Example, Kubernetes, Azure Container Apps and AWS Elastic Container Service.</p>
        </div>
      </div>
    </section>
  </EndpointSettingsSupported>
</template>

<style scoped>
.instructions {
  padding: 10px;
}

.instructions p {
  color: unset;
}
.defaultSetting {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 1em;
  line-height: 1em;
}

.defaultSetting .switch {
  margin-top: -8px;
}

.instructions > div {
  margin-bottom: 5px;
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

.icon {
  color: var(--reduced-emphasis);
}
</style>
