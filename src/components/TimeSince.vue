<script setup lang="ts">
import { onMounted, onUnmounted, ref } from "vue";
import moment from "moment";

const emptyDate = "0001-01-01T00:00:00";

const props = withDefaults(defineProps<{ dateUtc?: string; defaultTextOnFailure?: string; titleValue?: string }>(), { dateUtc: emptyDate, defaultTextOnFailure: "n/a", titleValue: undefined });

let interval: number | undefined = undefined;

const title = ref(),
  text = ref();

function updateText() {
  if (props.dateUtc != null && props.dateUtc !== emptyDate) {
    const m = moment.utc(props.dateUtc);
    text.value = m.fromNow();
    title.value = props.titleValue ?? m.local().format("LLLL") + " (local)\n" + m.utc().format("LLLL") + " (UTC)";
  } else {
    text.value = props.defaultTextOnFailure;
    title.value = props.titleValue ?? props.defaultTextOnFailure;
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
