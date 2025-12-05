<script setup lang="ts">
import ConditionalRender from "@/components/ConditionalRender.vue";
import useConnectionsAndStatsAutoRefresh from "@/composables/useConnectionsAndStatsAutoRefresh";
import { useServiceControlStore } from "@/stores/ServiceControlStore";
import { storeToRefs } from "pinia";

const { store: connectionStore } = useConnectionsAndStatsAutoRefresh();
const connectionState = connectionStore.connectionState;
const serviceControlStore = useServiceControlStore();
const { serviceControlUrl } = storeToRefs(serviceControlStore);
</script>

<template>
  <div class="sp-loader" v-if="connectionState.connecting && !connectionState.unableToConnect" />
  <ConditionalRender v-else :supported="!connectionState.unableToConnect">
    <template #unsupported>
      <div class="text-center unsupported">
        <h1>Cannot connect to ServiceControl</h1>
        <p>
          ServicePulse is unable to connect to the ServiceControl instance at
          <span id="serviceControlUrl">{{ serviceControlUrl }}</span
          >. Please ensure that ServiceControl is running and accessible from your machine.
        </p>
      </div>
    </template>
    <slot />
  </ConditionalRender>
</template>

<style scoped>
.action-toolbar {
  display: flex;
  gap: 0.5em;
  justify-content: center;
}
.sp-loader {
  width: 100%;
  height: 90vh;
  margin-top: -6.25em;
  background-image: url("@/assets/sp-loader.gif");
  background-size: 9.375em 9.375em;
  background-position: center center;
  background-repeat: no-repeat;
}
.unsupported {
  margin: 3.75em auto 7.5em;
  max-width: 32.5em;
  line-height: 1.625em;
}

.unsupported h1 {
  font-size: 1.875em;
}
.unsupported p {
  font-size: 1em;
  margin-bottom: 1.25em;
  margin-top: -1.125em;
}

.unsupported ul {
  padding-left: 0;
  text-align: left;
  font-size: 1em;
  margin-bottom: 1.875em;
}

.unsupported .btn {
  font-size: 1em;
}

.unsupported a.btn.btn-default.btn-secondary {
  margin-left: 0.625em;
}
</style>
