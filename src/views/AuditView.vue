<script setup lang="ts">
import AuditList from "@/components/audit/AuditList.vue";
import useIsAllMessagesSupported, { minimumSCVersionForAllMessages } from "@/components/audit/isAllMessagesSupported";
import ConditionalRender from "@/components/ConditionalRender.vue";
import ServiceControlAvailable from "@/components/ServiceControlAvailable.vue";
import LicenseNotExpired from "@/components/LicenseNotExpired.vue";

const isAllMessagesSupported = useIsAllMessagesSupported();
</script>

<template>
  <ServiceControlAvailable>
    <LicenseNotExpired>
      <ConditionalRender :supported="isAllMessagesSupported">
        <template #unsupported>
          <div class="not-supported">
            <p>
              The minimum version of ServiceControl required to enable this feature is
              <span> {{ minimumSCVersionForAllMessages }} </span>.
            </p>
            <div>
              <a class="btn btn-default btn-primary" href="https://particular.net/downloads" target="_blank">Update ServiceControl to latest version</a>
            </div>
          </div>
        </template>

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
      </ConditionalRender>
    </LicenseNotExpired>
  </ServiceControlAvailable>
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

.not-supported {
  display: flex;
  align-items: center;
  justify-content: center;
  flex-direction: column;
}
</style>
