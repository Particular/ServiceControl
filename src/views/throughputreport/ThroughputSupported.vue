<script setup lang="ts">
import ConditionalRender from "@/components/ConditionalRender.vue";
import { useIsSupported } from "@/composables/serviceSemVer";
import { environment } from "@/composables/serviceServiceControl";
import { computed } from "vue";

const minimumSCVersionForThroughput = "5.0.0";
const isThroughputSupported = computed(() => useIsSupported(environment.sc_version, minimumSCVersionForThroughput));
</script>

<template>
  <ConditionalRender :supported="isThroughputSupported">
    <template #unsupported>
      <div class="container">
        <div class="row">
          <div class="col-sm-12">
            <h1>Usage</h1>
          </div>
        </div>
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
.message {
  margin: 60px auto 120px;
  max-width: 520px;
  line-height: 26px;
}

.message h1 {
  font-size: 30px;
}
.message p {
  font-size: 16px;
  margin-bottom: 20px;
  margin-top: -18px;
}

.message ul {
  padding-left: 0;
  text-align: left;
  font-size: 16px;
  margin-bottom: 30px;
}

.message .btn {
  font-size: 16px;
  margin-left: 10px;
}
</style>
