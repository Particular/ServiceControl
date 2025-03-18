<script setup lang="ts">
import { licenseStatus } from "../composables/serviceLicense";
import LicenseExpired from "../components/LicenseExpired.vue";
import AuditList from "@/components/audit/AuditList.vue";
</script>

<template>
  <LicenseExpired />
  <template v-if="!licenseStatus.isExpired">
    <div class="container">
      <div class="row title">
        <div class="col-12">
          <h1>All Messages</h1>
        </div>
      </div>
      <div class="row">
        <AuditList />
      </div>
    </div>
  </template>
</template>

<style scoped>
.container,
.row {
  display: flex;
  flex-direction: column;
  max-height: 100%;
  flex: 1;
  min-height: 0;
}

.row.title {
  flex: 0;
  min-height: fit-content;
}
</style>
