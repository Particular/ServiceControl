<script setup lang="ts">
import { onMounted, onUnmounted, ref } from "vue";
import moment from "moment";
import { Tippy } from "vue-tippy";

const emptyDate = "0001-01-01T00:00:00";

const props = withDefaults(defineProps<{ dateUtc?: string; defaultTextOnFailure?: string; titleValue?: string }>(), { dateUtc: emptyDate, defaultTextOnFailure: "n/a", titleValue: undefined });

let interval: number | undefined = undefined;

const title = ref<string[]>([]),
  text = ref();

function updateText() {
  if (props.dateUtc != null && props.dateUtc !== emptyDate) {
    const m = moment.utc(props.dateUtc);
    text.value = m.fromNow();
    title.value = props.titleValue ? [props.titleValue] : [`${m.local().format("LLLL")} (local)`, `${m.utc().format("LLLL")} (UTC)`];
  } else {
    text.value = props.defaultTextOnFailure;
    title.value = [props.titleValue ?? props.defaultTextOnFailure];
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
  <Tippy>
    <template #content>
      <div v-for="row in title" :key="row">{{ row }}</div>
    </template>
    <span>{{ text }}</span>
  </Tippy>
</template>
