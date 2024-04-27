<script setup lang="ts">
import NoData from "@/components/NoData.vue";
import CustomCheckView from "@/components/customchecks/CustomCheckView.vue";
import { useCustomChecksStore } from "@/stores/CustomChecksStore";
import { storeToRefs } from "pinia";
import PaginationStrip from "@/components/PaginationStrip.vue";

const store = useCustomChecksStore();

const { pageNumber, failingCount, failedChecks } = storeToRefs(store);
</script>

<template>
  <div class="container">
    <div class row="row">
      <div class="col-sm-12 padded">
        <h1>Custom checks</h1>
      </div>
    </div>

    <section name="custom_checks">
      <NoData v-if="failingCount === 0" message="No failed custom checks" />
      <div v-else class="row">
        <div class="col-sm-12">
          <CustomCheckView v-for="item of failedChecks" :key="item.id" :custom-check="item" />
          <div class="row">
            <PaginationStrip :items-per-page="10" :total-count="failingCount" v-model="pageNumber" />
          </div>
        </div>
      </div>
    </section>
  </div>
</template>
