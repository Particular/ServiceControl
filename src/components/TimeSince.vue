<script setup lang="ts">
import { onMounted, onUnmounted, ref } from "vue";
import { useDateFormatter } from "@/composables/dateFormatter";

const props = withDefaults(
  defineProps<{
    dateUtc?: string;
    defaultTextOnFailure?: string;
    titleValue?: string;
  }>(),
  {
    dateUtc: "0001-01-01T00:00:00",
    defaultTextOnFailure: "n/a",
    titleValue: undefined,
  }
);

const { formatRelativeTime, formatDateTooltip, emptyDate } = useDateFormatter();

let interval: number | undefined = undefined;

const title = ref<string>("");
const text = ref<string>("");

function updateText() {
  if (props.dateUtc != null && props.dateUtc !== emptyDate) {
    text.value = formatRelativeTime(props.dateUtc, { emptyText: props.defaultTextOnFailure });
    title.value = formatDateTooltip(props.dateUtc, props.titleValue);
  } else {
    text.value = props.defaultTextOnFailure;
    title.value = props.titleValue ?? props.defaultTextOnFailure;
  }
}

onMounted(() => {
  interval = window.setInterval(updateText, 5000);
  updateText();
});

onUnmounted(() => window.clearInterval(interval));
</script>

<template>
  <span :title="title">{{ text }}</span>
</template>
