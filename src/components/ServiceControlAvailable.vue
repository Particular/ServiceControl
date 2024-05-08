<script setup lang="ts">
import ConditionalRender from "@/components/ConditionalRender.vue";
import { connectionState } from "@/composables/serviceServiceControl";
import routeLinks from "@/router/routeLinks";
import { serviceControlUrl } from "@/composables/serviceServiceControlUrls";
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
        <div class="action-toolbar">
          <RouterLink :to="routeLinks.configuration.connections.link"><span class="btn btn-default btn-primary whiteText">View Connection Details</span></RouterLink>
          <a class="btn btn-default btn-secondary" href="https://docs.particular.net/monitoring/metrics/">Learn more</a>
        </div>
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
  margin-top: -100px;
  background-image: url("@/assets/sp-loader.gif");
  background-size: 150px 150px;
  background-position: center center;
  background-repeat: no-repeat;
}
.unsupported {
  margin: 60px auto 120px;
  max-width: 520px;
  line-height: 26px;
}

.unsupported h1 {
  font-size: 30px;
}
.unsupported p {
  font-size: 16px;
  margin-bottom: 20px;
  margin-top: -18px;
}

.unsupported ul {
  padding-left: 0;
  text-align: left;
  font-size: 16px;
  margin-bottom: 30px;
}

.unsupported .btn {
  font-size: 16px;
}

.unsupported a.btn.btn-default.btn-secondary {
  margin-left: 10px;
}
</style>
