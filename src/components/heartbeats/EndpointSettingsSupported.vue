<script setup lang="ts">
import ConditionalRender from "@/components/ConditionalRender.vue";
import useIsEndpointSettingsSupported, { minimumSCVersionForEndpointSettings } from "@/components/heartbeats/isEndpointSettingsSupported";

const isEndpointSettingsSupported = useIsEndpointSettingsSupported();
</script>

<template>
  <ConditionalRender :supported="isEndpointSettingsSupported">
    <template #unsupported>
      <div class="container not-supported">
        <div class="row">
          <div class="col-sm-12">
            <div class="text-center message">
              <p>
                The minimum version of ServiceControl required to enable configuring tracking of endpoints is
                <span> {{ minimumSCVersionForEndpointSettings }} </span>.
              </p>
              <div>
                <a class="btn btn-default btn-primary" href="https://particular.net/downloads" target="_blank">Update ServiceControl to latest version</a>
              </div>
            </div>
          </div>
        </div>
      </div>
    </template>
    <slot />
  </ConditionalRender>
</template>

<style scoped>
@import "@/components/notsupported.css";
</style>
