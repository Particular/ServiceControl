<script setup lang="ts">
import { connectionState } from "@/composables/serviceServiceControl";
import { useIsThroughputSupported, minimumSCVersionForThroughput } from "@/views/throughputreport/IsThroughputSupported";
</script>

<template>
  <div class="sp-loader" v-if="connectionState.connecting && !connectionState.unableToConnect"></div>
  <template v-if="!connectionState.unableToConnect && !useIsThroughputSupported()">
    <div class="text-center monitoring-no-data">
      <h1>System Throughput</h1>
      <p>
        The minimum version of ServiceControl required to enable the System Throughput feaure is
        <span>{{ minimumSCVersionForThroughput }}</span
        >.
      </p>
      <div>
        <a class="btn btn-default btn-primary" href="https://particular.net/downloads" target="_blank">Update ServiceControl to latest version</a>
      </div>
    </div>
  </template>
</template>
