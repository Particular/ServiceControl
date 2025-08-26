<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import ThroughputConnectionSettings from "@/resources/ThroughputConnectionSettings";
import throughputClient from "@/views/throughputreport/throughputClient";
import { useIsMonitoringEnabled } from "@/composables/serviceServiceControlUrls";
import ConfigurationCode from "@/views/throughputreport/setup/ConfigurationCode.vue";
import { useThroughputStore } from "@/stores/ThroughputStore";
import { storeToRefs } from "pinia";
import FAIcon from "@/components/FAIcon.vue";
import { faCheck } from "@fortawesome/free-solid-svg-icons";

const store = useThroughputStore();
const { isBrokerTransport } = storeToRefs(useThroughputStore());
const settingsInfo = ref<ThroughputConnectionSettings | null>(null);

onMounted(async () => {
  settingsInfo.value = await throughputClient.setting();
});

const needsConfiguration = computed(() => {
  const broker = settingsInfo.value?.broker_settings?.length ?? 0;
  const monitoring = settingsInfo.value?.monitoring_settings?.length ?? 0;
  const serviceControl = settingsInfo.value?.service_control_settings?.length ?? 0;
  return broker > 0 || (monitoring > 0 && useIsMonitoringEnabled()) || serviceControl > 0;
});
</script>

<template>
  <div class="row">
    <p v-if="needsConfiguration">
      In order for ServicePulse to collect usage data from {{ store.transportNameForInstructions() }} you need to configure the below settings.<br />
      There are two configuration options, as environment variables or directly in the
      <a href="https://docs.particular.net/servicecontrol/creating-config-file"><code>ServiceControl.exe.config</code></a> file.
    </p>
    <p v-else>No further configuration required.</p>
  </div>
  <template v-if="settingsInfo?.broker_settings.length ?? 0 > 0">
    <div class="row configuration">
      <div class="col-12">
        <h4>Broker Settings</h4>
        <p class="nogap">
          Settings to ensure that usage data is being collected from <a :href="store.transportDocsLinkForInstructions()">{{ store.transportNameForInstructions() }}</a
          >.<br />
          Some settings can be automatically configured based on the current transport configuration, so if you have a <FAIcon :icon="faCheck" class="text-success" /> above it means that ServiceControl has successfully connected to
          {{ store.transportNameForInstructions() }}.
        </p>
        <ConfigurationCode :settings="settingsInfo?.broker_settings ?? []">
          <template #configInstructions>
            <div>Paste the settings above into the <code>ServiceControl.exe.config</code> file of the ServiceControl Error instance.</div>
          </template>
          <template #environmentVariableInstructions>
            <div>Execute the above instructions in a terminal to set the environment variables, these variables need to be set for the account under which the ServiceControl Error instance is running.</div>
          </template>
        </ConfigurationCode>
      </div>
    </div>
  </template>
  <template v-if="!isBrokerTransport">
    <template v-if="settingsInfo?.service_control_settings.length ?? 0 > 0">
      <div class="row configuration">
        <div class="col-12">
          <h4>ServiceControl Settings</h4>
          <p class="nogap">
            For more information read the
            <a href="https://docs.particular.net/servicecontrol/creating-config-file#usage-reporting-when-using-servicecontrol-licensingcomponentservicecontrolthroughputdataqueue">LicensingComponent/ServiceControlThroughputDataQueue</a> settings
            documentation.
          </p>
          <ConfigurationCode :settings="settingsInfo?.service_control_settings ?? []">
            <template #configInstructions>
              <div>Paste the settings above into the <code>ServiceControl.exe.config</code> file of the ServiceControl Error instance.</div>
            </template>
            <template #environmentVariableInstructions>
              <div>Execute the above instructions in a terminal to set the environment variables , these variables need to be set for the account under which the ServiceControl Error instance is running.</div>
            </template>
          </ConfigurationCode>
        </div>
      </div>
    </template>
    <template v-if="useIsMonitoringEnabled() && (settingsInfo?.monitoring_settings.length ?? 0 > 0)">
      <div class="row configuration">
        <div class="col-12">
          <h4>Monitoring Settings</h4>
          <p class="nogap">
            For more information read the
            <a href="https://docs.particular.net/servicecontrol/monitoring-instances/installation/creating-config-file#usage-reporting-monitoringservicecontrolthroughputdataqueue">Monitoring/ServiceControlThroughputDataQueue</a> settings
            documentation.
          </p>
          <ConfigurationCode :settings="settingsInfo?.monitoring_settings ?? []" configFileName="ServiceControl.Monitoring.exe.config">
            <template #configInstructions>
              <div>Paste the settings above into the <code>ServiceControl.Monitoring.exe.config</code> file of the ServiceControl Monitoring instance.</div>
            </template>
            <template #environmentVariableInstructions>
              <div>Execute the above instructions in a terminal to set the environment variables, these variables need to be set for the account under which the ServiceControl Monitoring instance is running.</div>
            </template>
          </ConfigurationCode>
        </div>
      </div>
    </template>
  </template>
</template>
<style scoped>
.configuration {
  margin-bottom: 15px;
}
.nogap {
  margin-bottom: 0;
}
</style>
