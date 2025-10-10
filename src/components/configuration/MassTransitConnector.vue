<script setup lang="ts">
import { useConfiguration } from "@/composables/configuration";
import { useDateFormatter } from "@/composables/dateFormatter";
import StatusIcon from "@/components/StatusIcon.vue";

const configuration = useConfiguration();
const { formatDate } = useDateFormatter();
</script>

<template>
  <div class="box" v-if="configuration?.mass_transit_connector !== undefined">
    <div class="row margin-bottom-10">
      <h4>
        Connector Version: <span class="version-format">{{ configuration.mass_transit_connector.version }}</span>
      </h4>
    </div>
    <div class="row margin-bottom-10">
      <h4>List of error queues configured in the connector.</h4>
      <div class="queues-container">
        <div class="margin-gap hover-highlight" v-for="queue in configuration.mass_transit_connector.error_queues" :key="queue.name">
          <StatusIcon :status="queue.ingesting ? 'success' : 'error'" :message="queue.ingesting ? '' : 'Not ingesting from this queue. Check the logs below for more information.'" :show-message="false" />
          <span>{{ queue.name }}</span>
        </div>
      </div>
    </div>
    <div class="row">
      <h4>The entries below are the most recent warning and error-level events recorded on the ServiceControl Connector.</h4>
      <div class="logs-container">
        <div v-if="configuration.mass_transit_connector.logs.length === 0">No warning or error logs</div>
        <div v-else class="row margin-gap hover-highlight" v-for="log in [...configuration.mass_transit_connector.logs].reverse()" :key="log.date">
          <div class="col-2">{{ formatDate(log.date) }}</div>
          <div class="col-1" :class="`${log.level.toLowerCase()}-color`">{{ log.level }}</div>
          <div class="col-9" :class="`${log.level.toLowerCase()}-color`">
            <pre>{{ log.message }}</pre>
          </div>
        </div>
      </div>
    </div>
  </div>
  <div class="box" v-else>
    <p>MassTransit Connector for ServiceControl is not configured.</p>
    <p><a target="_blank" href="https://particular.net/learn-more-about-masstransit-connector">Learn more about the MassTransit Connector.</a></p>
  </div>
</template>

<style scoped>
.hover-highlight:hover {
  background-color: #ededed;
}

.margin-gap {
  margin-bottom: 3px;
}

.queues-container {
  padding: 0.75rem;
}
.queues-container > div {
  display: flex;
  align-items: center;
  gap: 0.5em;
}
.queues-container > div div {
  overflow-wrap: anywhere;
}

.logs-container {
  padding: 0.75rem;
}
.version-format {
  font-weight: bold;
}
.box > .row:not(:last-child) {
  padding-bottom: 0.5rem;
  border-bottom: 1px solid #ccc;
  margin-bottom: 0.5rem;
}

.logs-container pre {
  all: revert;
  margin: 0;
  font-size: 0.9rem;
  overflow-wrap: break-word;
  text-wrap: auto;
}

.warning-color {
  color: var(--bs-warning);
}
.error-color {
  color: var(--bs-danger);
}
.info-color {
  color: var(--bs-success);
}
</style>
