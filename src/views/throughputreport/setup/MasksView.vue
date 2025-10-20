<script setup lang="ts">
import { onMounted, ref } from "vue";
import throughputClient from "@/views/throughputreport/throughputClient";
import { useShowToast } from "@/composables/toast";
import { TYPE } from "vue-toastification";
import { useEnvironmentAndVersionsStore } from "@/stores/EnvironmentAndVersionsStore";
import useIsThroughputSupported from "../isThroughputSupported";

const masks = ref<string>("");
const separator = "\n";

const environmentStore = useEnvironmentAndVersionsStore();
const isThroughputSupported = useIsThroughputSupported();

onMounted(async () => {
  if (environmentStore.environment.sc_version === "") await environmentStore.refresh();
  //needs to be after the environment refresh since it uses the sc_version
  if (!isThroughputSupported.value) return;
  const maskArray = await throughputClient.getMasks();
  masks.value = maskArray.join(separator);
});

function masksChanged(event: Event) {
  masks.value = (event.target as HTMLInputElement).value;
}

async function updateMasks() {
  const values = masks.value.split(separator).filter((value) => value.length > 0);

  await throughputClient.updateMasks(values);

  useShowToast(TYPE.SUCCESS, "Masks Saved", "");
}
</script>

<template>
  <div class="row">
    <div class="col-6">
      <div>
        <p>
          The report that is generated will contain the names of endpoints/queues.<br />
          If the names themselves contain confidential or proprietary information, certain strings can be masked in the report file.
        </p>
      </div>
    </div>
  </div>
  <div class="row">
    <div class="col-6">
      <label class="form-label">List of words to mask</label>
      <textarea class="form-control" aria-label="List of words to mask" rows="3" :value="masks" @input="masksChanged"></textarea>
      <div class="form-text">One word per line.</div>
    </div>
  </div>
  <div class="row">
    <div class="col-6">
      <br />
      <button class="btn btn-primary" type="button" @click="updateMasks" aria-label="Save">Save</button>
    </div>
  </div>
</template>

<style scoped></style>
