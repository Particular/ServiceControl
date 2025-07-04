<script setup lang="ts">
import { ref } from "vue";
import LicenseExpired from "../LicenseExpired.vue";
import { licenseStatus } from "@/composables/serviceLicense";
import { monitoringUrl as configuredMonitoringUrl, serviceControlUrl as configuredServiceControlUrl, updateServiceControlUrls, useIsMonitoringDisabled } from "../../composables/serviceServiceControlUrls";
import { connectionState, monitoringConnectionState } from "@/composables/serviceServiceControl";
import { faCheck, faExclamationTriangle } from "@fortawesome/free-solid-svg-icons";
import FAIcon from "@/components/FAIcon.vue";

const isExpired = licenseStatus.isExpired;

const serviceControlUrl = ref(configuredServiceControlUrl.value);
const monitoringUrl = ref(configuredMonitoringUrl.value);

const testingServiceControl = ref(false);
const serviceControlValid = ref<boolean | null>(null);

const testingMonitoring = ref(false);
const monitoringValid = ref<boolean | null>(null);

const connectionSaved = ref<boolean | null>(null);

async function testServiceControlUrl() {
  if (serviceControlUrl.value) {
    testingServiceControl.value = true;
    try {
      const response = await fetch(serviceControlUrl.value);
      serviceControlValid.value = response.ok && response.headers.has("X-Particular-Version");
    } catch {
      serviceControlValid.value = false;
    } finally {
      testingServiceControl.value = false;
    }
  }
}

async function testMonitoringUrl() {
  if (monitoringUrl.value) {
    testingMonitoring.value = true;

    if (!monitoringUrl.value.endsWith("/") && monitoringUrl.value !== "!") {
      monitoringUrl.value += "/";
    }

    try {
      const response = await fetch(monitoringUrl.value + "monitored-endpoints");
      monitoringValid.value = response.ok && response.headers.has("X-Particular-Version");
    } catch {
      monitoringValid.value = false;
    } finally {
      testingMonitoring.value = false;
    }
  }
}

function isMonitoringUrlSpecified() {
  return monitoringUrl.value && monitoringUrl.value !== "!";
}

function saveConnections() {
  updateServiceControlUrls(serviceControlUrl, monitoringUrl);
  connectionSaved.value = true;
}
</script>

<template>
  <LicenseExpired />
  <template v-if="!isExpired">
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
                  <input type="text" id="serviceControlUrl" name="serviceControlUrl" v-model="serviceControlUrl" class="form-control" style="color: #000" required />
                </div>

                <div class="col-5 no-side-padding">
                  <button class="btn btn-default btn-secondary btn-connection-test" :class="{ disabled: !configuredServiceControlUrl }" type="button" @click="testServiceControlUrl">Test</button>
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
                    <template v-if="monitoringConnectionState.unableToConnect && !useIsMonitoringDisabled()">
                      <span class="failed-validation"><FAIcon :icon="faExclamationTriangle" /> Unable to connect </span>
                    </template>
                  </label>
                  <input type="text" id="monitoringUrl" name="monitoringUrl" v-model="monitoringUrl" class="form-control" required />
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
