<script setup lang="ts">
import ConditionalRender from "@/components/ConditionalRender.vue";
import useIsThroughputSupported, { minimumSCVersionForThroughput } from "./isThroughputSupported";

const isThroughputSupported = useIsThroughputSupported();
</script>

<template>
  <ConditionalRender :supported="isThroughputSupported">
    <template #unsupported>
      <div class="container not-supported">
        <div class="row">
          <div class="col-sm-12">
            <div class="text-center message">
              <p>
                The minimum version of ServiceControl required to enable the Usage feature is
                <span> {{ minimumSCVersionForThroughput }} </span>.
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
