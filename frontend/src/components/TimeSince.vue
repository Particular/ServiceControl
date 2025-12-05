<script setup lang="ts">
import { onMounted, onUnmounted, ref, watch } from "vue";
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

//this will update the value immediately and if the props value changes
watch(() => props.dateUtc, updateText, { immediate: true });
//this updates the value according to time passed, e.g. timesince changing from 'moments ago' to 'a minute ago'
onMounted(() => {
  interval = window.setInterval(updateText, 5000);
});

onUnmounted(() => window.clearInterval(interval));
</script>

<template>
  <span :title="title">{{ text }}</span>
</template>
