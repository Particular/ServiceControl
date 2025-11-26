<script setup lang="ts">
import EventItemShort from "@/components/EventItemShort.vue";
import LicenseNotExpired from "@/components/LicenseNotExpired.vue";
import ServiceControlAvailable from "@/components/ServiceControlAvailable.vue";
import CustomChecksDashboardItem from "@/components/customchecks/CustomChecksDashboardItem.vue";
import HeartbeatsDashboardItem from "@/components/heartbeats/HeartbeatsDashboardItem.vue";
import FailedMessagesDashboardItem from "@/components/failedmessages/FailedMessagesDashboardItem.vue";
</script>

<template>
  <div class="container">
    <ServiceControlAvailable>
      <LicenseNotExpired>
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
      </LicenseNotExpired>
    </ServiceControlAvailable>
  </div>
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
