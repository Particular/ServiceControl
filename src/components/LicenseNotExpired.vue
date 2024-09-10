<script setup lang="ts">
import ConditionalRender from "@/components/ConditionalRender.vue";
import { licenseStatus } from "@/composables/serviceLicense";
import LicenseExpired from "@/components/LicenseExpired.vue";
</script>

<template>
  <ConditionalRender :supported="!licenseStatus.isExpired">
    <template #unsupported>
      <LicenseExpired />
    </template>
    <slot />
  </ConditionalRender>
</template>
