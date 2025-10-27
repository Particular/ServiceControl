<script setup lang="ts">
import routeLinks from "@/router/routeLinks";
import { useServiceControlStore } from "@/stores/ServiceControlStore";
import { storeToRefs } from "pinia";

const store = useServiceControlStore();
const { monitoringUrl } = storeToRefs(store);
</script>

<template>
  <div class="row">
    <div class="col-sm-12">
      <div class="row">
        <div class="col-sm-12">
          <div class="row">
            <div class="col-sm-12">
              <div class="text-center monitoring-no-data">
                <h1>No monitoring connectivity</h1>
                <p>
                  Monitoring is not available at <strong>{{ monitoringUrl }}</strong>
                </p>
                <p>This may be due to one or more of the following causes:</p>
                <div class="monitoring-no-data-content">
                  <ol class="monitoring-no-data-list">
                    <li><strong>Monitoring</strong> has been disabled</li>
                    <li><strong>Monitoring server</strong> is not running.</li>
                    <li><strong>Monitoring server address</strong> is incorrect.</li>
                    <li><strong>Monitoring server</strong> not accessible. You may need to validate the configured hostname in the ServiceControl Management Utility.</li>
                  </ol>
                </div>
                <div class="action-toolbar">
                  <RouterLink :to="routeLinks.configuration.connections.link" class="btn btn-default btn-primary">View connection details</RouterLink>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
@import "./endpoint.css";
</style>
