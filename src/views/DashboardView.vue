<script setup lang="ts">
import EventItemShort from "@/components/EventItemShort.vue";
import LicenseExpired from "@/components/LicenseExpired.vue";
import ServiceControlNotAvailable from "@/components/ServiceControlNotAvailable.vue";
import { connectionState } from "@/composables/serviceServiceControl";
import { licenseStatus } from "@/composables/serviceLicense";
import CustomChecksDashboardItem from "@/components/customchecks/CustomChecksDashboardItem.vue";
import HeartbeatsDashboardItem from "@/components/heartbeats/HeartbeatsDashboardItem.vue";
import FailedMessagesDashboardItem from "@/components/failedmessages/FailedMessagesDashboardItem.vue";
</script>

<template>
  <LicenseExpired />
  <template v-if="!licenseStatus.isExpired">
    <div class="container">
      <ServiceControlNotAvailable />

      <template v-if="connectionState.connected">
        <div class="row">
          <div class="col-12">
            <h6>System status</h6>
            <div class="row box system-status">
              <div class="col-12">
                <div class="row">
                  <div class="system-status-item">
                    <HeartbeatsDashboardItem />
                  </div>
                  <div class="system-status-item">
                    <FailedMessagesDashboardItem />
                  </div>
                  <div class="system-status-item">
                    <CustomChecksDashboardItem />
                  </div>
                </div>
              </div>
            </div>
          </div>
          <EventItemShort></EventItemShort>
        </div>
      </template>
    </div>
  </template>
</template>

<style scoped>
.system-status:hover {
  background-color: #fff;
  border-color: #eee !important;
  cursor: default;
}

.system-status .row {
  display: flex;
}

.system-status-item {
  flex: 1;
}
</style>
