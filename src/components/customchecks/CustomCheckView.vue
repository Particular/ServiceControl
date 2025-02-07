<script setup lang="ts">
import CustomCheck from "@/resources/CustomCheck";
import TimeSince from "@/components/TimeSince.vue";
import { useCustomChecksStore } from "@/stores/CustomChecksStore";

defineProps<{ customCheck: CustomCheck }>();

const store = useCustomChecksStore();
</script>

<template>
  <div class="row box box-warning box-no-click">
    <div class="col-sm-12 no-side-padding">
      <div class="custom-check-row" role="row" aria-label="custom-check-failed-row">
        <div class="custom-check-row-detail">
          <div class="row box-header">
            <div class="col-sm-12 no-side-padding">
              <p class="lead" role="note" aria-label="custom-check-failed-reason">{{ customCheck.failure_reason }}</p>
              <div class="row">
                <div class="col-sm-12 no-side-padding">
                  <p class="metadata">
                    <span class="metadata"><i aria-hidden="true" class="fa fa-check"></i> Check: {{ customCheck.custom_check_id }}</span>
                    <span class="metadata"><i aria-hidden="true" class="fa fa-list"></i> Category: {{ customCheck.category }}</span>
                    <span class="metadata"><i aria-hidden="true" class="fa pa-endpoint"></i> Endpoint: {{ customCheck.originating_endpoint.name }}</span>
                    <span class="metadata"><i aria-hidden="true" class="fa fa-server"></i> Host: {{ customCheck.originating_endpoint.host }}</span>
                    <span class="metadata"><i aria-hidden="true" class="fa fa-clock-o"></i> Last checked: <TimeSince :date-utc="customCheck.reported_at" role="note" aria-label="custom-check-reported-date"></TimeSince></span>
                  </p>
                </div>
              </div>
            </div>
          </div>
        </div>
        <div>
          <button type="button" class="btn btn-default" title="Dismiss this custom check so it doesn't show up as an alert" role="button" aria-label="custom-check-dismiss" @click="store.dismissCustomCheck(customCheck.id)">Dismiss</button>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
@import "@/components/list.css";

.custom-check-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 1em;
}

.custom-check-row-detail {
  min-width: 0;
}

.custom-check-row-detail .lead {
  text-wrap: wrap;
}
</style>
