<script setup lang="ts">
import { ref } from "vue";
import { faCheck, faExclamationTriangle } from "@fortawesome/free-solid-svg-icons";
import FAIcon from "@/components/FAIcon.vue";
import useConnectionsAndStatsAutoRefresh from "@/composables/useConnectionsAndStatsAutoRefresh";
import { useServiceControlStore } from "@/stores/ServiceControlStore";
import { storeToRefs } from "pinia";

const { store: connectionStore } = useConnectionsAndStatsAutoRefresh();
const connectionState = connectionStore.connectionState;
const monitoringConnectionState = connectionStore.monitoringConnectionState;

const serviceControlStore = useServiceControlStore();
serviceControlStore.refresh();
const localServiceControlUrl = ref(serviceControlStore.serviceControlUrl);
const localMonitoringUrl = ref(serviceControlStore.monitoringUrl);
const { isMonitoringDisabled } = storeToRefs(serviceControlStore);

const testingServiceControl = ref(false);
const serviceControlValid = ref<boolean | null>(null);

const testingMonitoring = ref(false);
const monitoringValid = ref<boolean | null>(null);

const connectionSaved = ref<boolean | null>(null);

async function testServiceControlUrl() {
  if (localServiceControlUrl.value) {
    testingServiceControl.value = true;
    try {
      const response = await fetch(localServiceControlUrl.value);
      serviceControlValid.value = response.ok && response.headers.has("X-Particular-Version");
    } catch {
      serviceControlValid.value = false;
    } finally {
      testingServiceControl.value = false;
    }
  }
}

async function testMonitoringUrl() {
  if (localMonitoringUrl.value) {
    testingMonitoring.value = true;

    if (!localMonitoringUrl.value.endsWith("/") && localMonitoringUrl.value !== "!") {
      localMonitoringUrl.value += "/";
    }

    try {
      const response = await fetch(localMonitoringUrl.value + "monitored-endpoints");
      monitoringValid.value = response.ok && response.headers.has("X-Particular-Version");
    } catch {
      monitoringValid.value = false;
    } finally {
      testingMonitoring.value = false;
    }
  }
}

function isMonitoringUrlSpecified() {
  return localMonitoringUrl.value && localMonitoringUrl.value !== "!";
}

function saveConnections() {
  updateServiceControlUrls();
  connectionSaved.value = true;
}

function updateServiceControlUrls() {
  if (!localServiceControlUrl.value) {
    throw new Error("ServiceControl URL is mandatory");
  } else if (!localServiceControlUrl.value.endsWith("/")) {
    localServiceControlUrl.value += "/";
  }

  if (!localMonitoringUrl.value) {
    localMonitoringUrl.value = "!"; //disabled
  } else if (!localMonitoringUrl.value.endsWith("/") && localMonitoringUrl.value !== "!") {
    localMonitoringUrl.value += "/";
  }

  //values have changed. They'll be reset after page reloads
  window.localStorage.removeItem("scu");
  window.localStorage.removeItem("mu");

  const newSearch = `?scu=${localServiceControlUrl.value}&mu=${localMonitoringUrl.value}`;
  console.debug("updateConnections - new query string: ", newSearch);
  window.location.search = newSearch;
}
</script>

<template>
  <section name="connections">
    <div class="box">
      <div class="row">
        <div class="col-12">
          <form novalidate>
            <div class="row connection">
              <h3>ServiceControl</h3>
              <div class="col-7 form-group">
                <label for="serviceControlUrl">
                  CONNECTION URL
                  <template v-if="connectionState.unableToConnect">
                    <span class="failed-validation"><FAIcon :icon="faExclamationTriangle" /> Unable to connect </span>
                  </template>
                </label>
                <input type="text" id="serviceControlUrl" name="serviceControlUrl" v-model="localServiceControlUrl" class="form-control" style="color: #000" required />
              </div>

              <div class="col-5 no-side-padding">
                <button class="btn btn-default btn-secondary btn-connection-test" :class="{ disabled: !localServiceControlUrl }" type="button" @click="testServiceControlUrl">Test</button>
                <span class="connection-test connection-testing" v-if="testingServiceControl"> <i class="glyphicon glyphicon-refresh rotate"></i>Testing </span>
                <span class="connection-test connection-successful" v-if="serviceControlValid === true && !testingServiceControl"><FAIcon :icon="faCheck" /> Connection successful </span>
                <span class="connection-test connection-failed" v-if="serviceControlValid === false && !testingServiceControl"><FAIcon :icon="faExclamationTriangle" /> Connection failed </span>
              </div>
            </div>

            <div class="row connection">
              <h3>ServiceControl Monitoring</h3>
              <div class="col-7 form-group">
                <label for="monitoringUrl">
                  CONNECTION URL
                  <span class="auxilliary-label">(OPTIONAL) (Enter ! to disable monitoring)</span>
                  <template v-if="monitoringConnectionState.unableToConnect && !isMonitoringDisabled">
                    <span class="failed-validation"><FAIcon :icon="faExclamationTriangle" /> Unable to connect </span>
                  </template>
                </label>
                <input type="text" id="monitoringUrl" name="monitoringUrl" v-model="localMonitoringUrl" class="form-control" required />
              </div>

              <div class="col-5 no-side-padding">
                <button class="btn btn-default btn-secondary btn-connection-test" :class="{ disabled: !isMonitoringUrlSpecified() }" type="button" @click="testMonitoringUrl" :disabled="!isMonitoringUrlSpecified()">Test</button>
                <span class="connection-test connection-testing" v-if="testingMonitoring"> <i class="glyphicon glyphicon-refresh rotate"></i>Testing </span>
                <span class="connection-test connection-successful" v-if="monitoringValid === true && !testingMonitoring"><FAIcon :icon="faCheck" /> Connection successful </span>
                <span class="connection-test connection-failed" v-if="monitoringValid === false && !testingMonitoring"><FAIcon :icon="faExclamationTriangle" /> Connection failed </span>
              </div>
            </div>

            <button class="btn btn-primary" type="button" @click="saveConnections">Save</button>
            <span class="connection-test connection-successful hide save-connection" v-show="connectionSaved"><FAIcon :icon="faCheck" /> Connection saved </span>
            <span class="connection-test connection-failed hide save-connection" v-show="connectionSaved != null && !connectionSaved"><FAIcon :icon="faExclamationTriangle" /> Unable to save </span>
          </form>
        </div>
      </div>
    </div>
  </section>
</template>

<style scoped>
section[name="connections"] .box {
  padding-bottom: 50px;
}

form .connection h3 {
  margin-bottom: 16px;
  margin-top: 25px;
  padding-left: 0;
}

form .connection .form-group {
  padding-left: 0;
}

.connection:nth-child(2) h3 {
  margin-top: 40px;
}

form .connection .form-group input {
  font-size: 16px;
  height: 44px;
}

.row.connection {
  margin-left: 0;
}

span.connection-test.save-connection {
  top: 0;
}

.btn-connection-test {
  margin-top: 25px;
  padding-top: 11px;
  padding-bottom: 11px;
}
</style>
