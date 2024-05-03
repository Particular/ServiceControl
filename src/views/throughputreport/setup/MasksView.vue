<script setup lang="ts">
import { onMounted, ref } from "vue";
import throughputClient from "@/views/throughputreport/throughputClient";
import { NewLineKind } from "typescript";
import { useShowToast } from "@/composables/toast";
import { TYPE } from "vue-toastification";

const masks = ref<string>("");

onMounted(async () => {
  const maskArray = await throughputClient.getMasks();
  masks.value = maskArray.toString().replaceAll(",", NewLineKind.LineFeed.toString());
});

function masksChanged(event: Event) {
  masks.value = (event.target as HTMLInputElement).value;
}

async function updateMasks() {
  const values = masks.value
    .split("\n")
    .filter((value) => value.length > 0)
    .map((value) => `${encodeURIComponent(value)}`);

  await throughputClient.updateMasks(values);

  useShowToast(TYPE.INFO, "Masks Saved", "");
}
</script>

<template>
  <div class="box">
    <div class="row">
      <div class="col-6">
        <label class="form-label">Hide sensitive data</label>
        <textarea class="form-control" rows="3" :value="masks" @input="masksChanged"></textarea>
        <div class="form-text">Hide sensitive information in the throughput report. One word per line.</div>
      </div>
    </div>
    <button class="btn btn-primary" type="button" @click="updateMasks">Save</button>
  </div>
</template>

<style scoped>
.extra-info {
  margin: 15px 0;
}
</style>
