<script setup lang="ts">
import { onMounted, ref } from "vue";
import LicenseExpired from "../LicenseExpired.vue";
import ServiceControlNotAvailable from "../ServiceControlNotAvailable.vue";
import { licenseStatus } from "@/composables/serviceLicense";
import BusyIndicator from "../BusyIndicator.vue";
import CodeEditor from "@/components/CodeEditor.vue";
import useConnectionsAndStatsAutoRefresh from "@/composables/useConnectionsAndStatsAutoRefresh";
import { monitoringUrl, serviceControlUrl, useTypedFetchFromMonitoring, useTypedFetchFromServiceControl } from "@/composables/serviceServiceControlUrls";

interface ServiceControlInstanceConnection {
  settings: { [key: string]: object };
  errors: string[];
}

interface MetricsConnectionDetails {
  Enabled: boolean;
  MetricsQueue?: string;
  Interval?: string;
}

const { store: connectionStore } = useConnectionsAndStatsAutoRefresh();
const connectionState = connectionStore.connectionState;
const isExpired = licenseStatus.isExpired;

const loading = ref(true);
const showCodeOnlyTab = ref(true);
const jsonSnippet = ref("");
const inlineSnippet = ref("");
const jsonConfig = ref("");
const queryErrors = ref<string[]>([]);

async function getCode() {
  loading.value = true;

  const snippetTemplate = `var servicePlatformConnection = ServicePlatformConnectionConfiguration.Parse(@"<json>");

    endpointConfiguration.ConnectToServicePlatform(servicePlatformConnection);
    `;

  jsonSnippet.value = `var json = File.ReadAllText("<path-to-json-file>.json");
var servicePlatformConnection = ServicePlatformConnectionConfiguration.Parse(json);
endpointConfiguration.ConnectToServicePlatform(servicePlatformConnection);
`;
  const connections = await serviceControlConnections();
  const config = {
    Heartbeats: connections.serviceControl.settings.Heartbeats,
    CustomChecks: connections.serviceControl.settings.CustomChecks,
    ErrorQueue: connections.serviceControl.settings.ErrorQueue,
    SagaAudit: connections.serviceControl.settings.SagaAudit,
    MessageAudit: connections.serviceControl.settings.MessageAudit,
    Metrics: connections.monitoring.settings,
  };
  let jsonText = JSON.stringify(config, null, 4);
  jsonConfig.value = jsonText;

  jsonText = jsonText.replaceAll('"', '""');
  inlineSnippet.value = snippetTemplate.replace("<json>", jsonText);

  queryErrors.value = [];
  queryErrors.value = queryErrors.value.concat(connections.serviceControl.errors || []);
  queryErrors.value = queryErrors.value.concat(connections.monitoring.errors || []);

  loading.value = false;
}

onMounted(async () => {
  await getCode();
});

function switchCodeOnlyTab() {
  showCodeOnlyTab.value = true;
}

function switchJsonTab() {
  showCodeOnlyTab.value = false;
}

async function serviceControlConnections() {
  const scConnectionResult = getServiceControlConnection();
  const monitoringConnectionResult = getMonitoringConnection();

  const [scConnection, mConnection] = await Promise.all([scConnectionResult, monitoringConnectionResult]);
  return {
    serviceControl: {
      settings: scConnection?.settings ?? {},
      errors: scConnection?.errors ?? [],
    } as ServiceControlInstanceConnection,
    monitoring: {
      settings: mConnection?.Metrics ?? ({ Enabled: false } as MetricsConnectionDetails),
      errors: mConnection?.errors ?? [],
    },
  };
}

async function getServiceControlConnection() {
  try {
    const [, data] = await useTypedFetchFromServiceControl<ServiceControlInstanceConnection>("connection");
    return data;
  } catch {
    return { errors: [`Error reaching ServiceControl at ${serviceControlUrl.value} connection`] } as ServiceControlInstanceConnection;
  }
}

async function getMonitoringConnection() {
  try {
    const [, data] = await useTypedFetchFromMonitoring<{ Metrics: MetricsConnectionDetails }>("connection");
    return { ...data, errors: [] };
  } catch {
    return { Metrics: null, errors: [`Error SC Monitoring instance at ${monitoringUrl.value}connection`] };
  }
}
</script>

<template>
  <LicenseExpired />
  <template v-if="!isExpired">
    <section name="platformconnection">
      <ServiceControlNotAvailable />
      <template v-if="!connectionState.unableToConnect">
        <div class="box configuration">
          <div class="row">
            <div class="col-12">
              <h3>Connect an endpoint to ServiceControl</h3>
            </div>
          </div>
          <div class="row">
            <div class="col-12">
              <ol>
                <li>Add the <a href="https://www.nuget.org/packages/NServiceBus.ServicePlatform.Connector/">NServiceBus.ServicePlatform.Connector</a> NuGet package to the endpoint project.</li>
                <li>Copy-paste the code from one of the options below. For additional options, refer to the <a href="https://docs.particular.net/platform/connecting">documentation</a></li>
              </ol>
            </div>
          </div>
          <div class="row tabs-config-snippets">
            <div class="col-12">
              <busy-indicator v-show="loading"></busy-indicator>

              <!-- Nav tabs -->
              <div v-if="!loading" class="tabs" role="tablist">
                <h5 :class="{ active: showCodeOnlyTab }">
                  <a @click="switchCodeOnlyTab()" class="ng-binding">Endpoint configuration only</a>
                </h5>
                <h5 :class="{ active: !showCodeOnlyTab }">
                  <a @click="switchJsonTab()" class="ng-binding">JSON file</a>
                </h5>
              </div>

              <div v-if="queryErrors.length > 0 && !loading" class="alert alert-warning" role="alert">
                There were problems reaching some ServiceControl instances and the configuration does not contain all connectivity information.
                <ul>
                  <li v-for="error in queryErrors" :key="error">
                    {{ error }}
                  </li>
                </ul>
              </div>

              <section v-if="showCodeOnlyTab && !loading">
                <div class="row">
                  <div class="col-12 h-100">
                    <CodeEditor :model-value="inlineSnippet" language="csharp" :show-gutter="false"></CodeEditor>
                  </div>
                </div>
              </section>

              <section v-if="!showCodeOnlyTab && !loading">
                <div class="row">
                  <div class="col-12 h-100">
                    <p>Note that when using JSON for configuration, you also need to change the endpoint configuration as shown below.</p>
                    <p><strong>Endpoint configuration:</strong></p>
                    <CodeEditor :model-value="jsonSnippet" language="csharp" :show-gutter="false"></CodeEditor>
                    <p style="margin-top: 15px">
                      <strong>JSON configuration file:</strong>
                    </p>
                    <CodeEditor :model-value="jsonConfig" language="json" :show-gutter="false"></CodeEditor>
                  </div>
                </div>
              </section>
            </div>
          </div>
        </div>
      </template>
    </section>
  </template>
</template>

<style scoped>
.configuration :deep(pre) {
  border: none;
  background-color: #282c34;
}

.box > .row {
  margin-left: 0;
}

section[name="platformconnection"] ol {
  font-size: 16px;
  padding-left: 18px;
  margin: 15px 0 0;
}

section[name="platformconnection"] li {
  margin-bottom: 15px;
}

:deep(.code) {
  padding-bottom: 20px;
}

.tabs-config-snippets .tabs {
  margin: 30px 0 15px;
}

.tabs-config-snippets highlight {
  margin-bottom: 20px;
  display: block;
}

.tabs-config-snippets p {
  font-size: 16px;
  color: #181919;
}

.tabs-config-snippets .alert {
  margin-bottom: 15px;
}

.tabs-config-snippets .alert li {
  margin-bottom: 0;
}
</style>
