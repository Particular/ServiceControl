<script setup lang="ts">
import { onMounted, ref } from "vue";
import NoData from "../NoData.vue";
import { useTypedFetchFromServiceControl } from "../../composables/serviceServiceControlUrls";
import TimeSince from "../TimeSince.vue";
import type HistoricRetryOperation from "@/resources/HistoricRetryOperation";
import RecoverabilityHistoryResponse from "@/resources/RecoverabilityHistoryResponse";

const historicOperations = ref<HistoricRetryOperation[]>([]);
const showHistoricRetries = ref(false);

async function getHistoricOperations() {
  const [, data] = await useTypedFetchFromServiceControl<RecoverabilityHistoryResponse>("recoverability/history");
  historicOperations.value = data.historic_operations;
}

onMounted(() => {
  getHistoricOperations();
});
</script>

<template>
  <div class="lasttenoperations">
    <div class="row">
      <div class="col-sm-12 list-section">
        <h6>
          <span class="no-link-underline" aria-hidden="true" v-show="showHistoricRetries"><i class="fa fa-angle-down" aria-hidden="true"></i> </span>
          <span class="fake-link" aria-hidden="true" v-show="!showHistoricRetries"><i class="fa fa-angle-right" aria-hidden="true"></i> </span>
          <a class="lastTenHeading" v-on:click="showHistoricRetries = !showHistoricRetries"> Last 10 completed retry requests</a>
        </h6>
      </div>
    </div>

    <div class="row">
      <div class="col-sm-12 no-mobile-side-padding" v-show="showHistoricRetries">
        <no-data v-if="historicOperations.length === 0" title="message group retries" message="No group retry requests have ever been completed"></no-data>
        <div class="row box extra-box-padding repeat-modify" v-for="(group, index) in historicOperations" :key="index" v-show="historicOperations.length">
          <div class="col-sm-12 no-mobile-side-padding">
            <div class="row">
              <div class="col-sm-12 no-side-padding">
                <div class="row box-header">
                  <div class="col-sm-12 no-side-padding">
                    <p class="lead break">{{ group.originator || "Selection of individual message(s)" }}</p>
                  </div>
                </div>

                <div class="row">
                  <div class="col-sm-12 no-side-padding">
                    <p class="metadata">
                      <span class="metadata"><i aria-hidden="true" class="fa fa-envelope"></i> Messages sent: {{ group.number_of_messages_processed }} </span>
                      <span class="metadata"
                        ><i aria-hidden="true" class="fa fa-clock-o"></i> Retry request started:
                        <time-since :date-utc="group.start_time"></time-since>
                      </span>
                      <span class="metadata"
                        ><i aria-hidden="true" class="fa fa-clock-o"></i> Retry request completed:
                        <time-since :date-utc="group.completion_time"></time-since>
                      </span>
                    </p>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
        <span class="short-group-history" v-show="historicOperations.length === 1">There is only {{ historicOperations.length }} completed group retry</span>
        <span class="short-group-history" v-show="historicOperations.length < 10 && historicOperations.length > 1">There are only {{ historicOperations.length }} completed group retries</span>
      </div>
    </div>
  </div>
</template>

<style>
.fake-link i {
  padding-right: 0.2em;
}

.lasttenoperations {
  padding-bottom: 2em;
}

.lasttenoperations > div > div > h6 {
  margin-top: 10px;
  margin-bottom: 10px;
}

.lastTenHeading {
  color: #00a3c4;
}
</style>
