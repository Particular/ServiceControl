<script setup lang="ts">
import { onMounted, onUnmounted, ref } from "vue";
import moment from "moment";

const emptyDate = "0001-01-01T00:00:00";

const props = withDefaults(defineProps<{ dateUtc: string }>(), { dateUtc: emptyDate });

let interval: number | undefined = undefined;

const title = ref(),
  text = ref();

function updateText() {
  if (props.dateUtc != null && props.dateUtc !== emptyDate) {
    const m = moment.utc(props.dateUtc);
    text.value = m.fromNow();
    title.value = m.local().format("LLLL") + " (local)\n" + m.utc().format("LLLL") + " (UTC)";
  } else {
    text.value = "n/a";
    title.value = "n/a";
  }
}

onMounted(() => {
  interval = window.setInterval(function () {
    updateText();
  }, 5000);

  updateText();
});

onUnmounted(() => window.clearInterval(interval));
</script>

<template>
  <span :title="title">{{ text }}</span>
</template>

<style></style>
