<script setup lang="ts">
import ConditionalRender from "@/components/ConditionalRender.vue";
import LicenseExpired from "@/components/LicenseExpired.vue";
import { useLicenseStore } from "@/stores/LicenseStore";

const licenseStore = useLicenseStore();
const { licenseStatus } = licenseStore;
</script>

<template>
  <ConditionalRender :supported="!licenseStatus.isExpired">
    <template #unsupported>
      <LicenseExpired />
    </template>
    <slot />
  </ConditionalRender>
</template>
