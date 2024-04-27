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
                  <div class="col-4">
                    <HeartbeatsDashboardItem />
                  </div>
                  <div class="col-4">
                    <FailedMessagesDashboardItem />
                  </div>
                  <div class="col-4">
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

<style>
.system-status:hover {
  background-color: #fff;
  border-color: #eee !important;
  cursor: default;
}

.summary-item {
  background: #fff none repeat scroll 0 0;
  border: 1px solid #fff;
  border-radius: 4px;
  color: #777f7f;
  display: block;
  padding: 25px 10px;
  position: relative;
  text-align: center;
}

.summary-item .badge,
.summary-item .label {
  font-size: 18px;
  margin-left: 12px;
  position: absolute;
  top: 2px;
}

.summary-info,
.summary-info > .fa,
a.summary-info:hover {
  color: #777f7f;
}

.summary-danger,
.summary-danger > .fa,
a.summary-danger:hover {
  color: #ce4844;
  font-weight: bold;
}

.summary-item:hover {
  background-color: #edf6f7;
  border-color: #00a3c4 !important;
  cursor: pointer;
}
</style>
