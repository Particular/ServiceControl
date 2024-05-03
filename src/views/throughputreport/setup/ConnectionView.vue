<script setup lang="ts">
import ConnectionTestResults from "@/resources/ConnectionTestResults";
import ThroughputConnectionSettings from "@/resources/ThroughputConnectionSettings";

const props = defineProps<{
  testResults: ConnectionTestResults | null;
  transportDisplayName: string | undefined;
  settingsInfo: ThroughputConnectionSettings | null;
}>();

const emit = defineEmits<{
  test: [];
}>();

function testConnection() {
  emit("test");
}
</script>

<template>
  <div class="box">
    <div class="row">
      <div class="intro">
        <p>In order for ServicePulse to collect throughput data directly from {{ $props.transportDisplayName }} you need to setup the following settings in ServiceControl.</p>
        <p>There are two options to set the settings, you can either set environment variables or alternative is to set it directly in the <code>ServiceControl.exe.config</code> file.</p>
        <p>For more information read <a href="https://docs.particular.net/servicecontrol/creating-config-file">this documentation</a>.</p>
      </div>
    </div>
  </div>
  <div class="row"><button class="btn btn-primary actions" type="button" @click="testConnection">Test Connection</button></div>
  <div class="row">
    <div class="card">
      <div class="card-body">
        <h5 v-if="props.settingsInfo?.broker_settings?.length! > 0">Broker Settings</h5>
        <ul class="card-text settingsList">
          <li v-for="item in props.settingsInfo?.broker_settings" :key="item.name">
            <div>
              <strong>{{ item.name }}</strong>
            </div>
            <p>{{ item.description }}</p>
          </li>
        </ul>
        <h5 class="card-title">ServiceControl Settings</h5>
        <p>Settings to ensure that throughput data is being collected from Monitoring and Audit.</p>
        <p>For more information read <a href="TODO">this documentation</a>.</p>
        <ul class="card-text settingsList">
          <li v-for="item in props.settingsInfo?.service_control_settings" :key="item.name">
            <div>
              <strong>{{ item.name }}</strong>
            </div>
            <p>{{ item.description }}</p>
          </li>
        </ul>
      </div>
    </div>
  </div>
  <div class="row">
    <h3>Test Connection results</h3>
    <template v-if="props.settingsInfo?.broker_settings?.length! > 0">
      <h4>Broker</h4>
      <pre>{{ props.testResults?.broker_connection_result.diagnostics }}</pre>
    </template>
    <h4>Audit</h4>
    <pre>{{ props.testResults?.audit_connection_result.diagnostics }}</pre>
    <h4>Monitoring</h4>
    <pre>{{ props.testResults?.monitoring_connection_result.diagnostics }}</pre>
  </div>
</template>

<style scoped>
.settingsList {
  list-style: none;
  padding-left: 0;
}

.intro {
  margin: 10px 0;
}

.actions {
  margin: 10px 0;
  width: 200px;
}
</style>
