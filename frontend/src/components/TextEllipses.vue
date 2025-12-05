<script setup lang="ts">
import { ref, watch, onMounted } from "vue";

const props = withDefaults(
  defineProps<{
    text: string;
    ellipsesStyle?: "RightSide" | "LeftSide";
  }>(),
  { ellipsesStyle: "RightSide" }
);

const textContainer = ref<HTMLElement | null>(null);
const tooltipText = ref("");

const updateTooltip = () => {
  if (textContainer.value) {
    tooltipText.value = textContainer.value.scrollWidth > textContainer.value.clientWidth ? textContainer.value.textContent || "" : "";
  }
};

onMounted(() => {
  updateTooltip();
});

watch([() => props.text], () => {
  updateTooltip();
});
</script>

<template>
  <div ref="textContainer" title="" class="text-container hackToPreventSafariFromShowingTooltip" :class="{ 'left-side-ellipsis': ellipsesStyle === 'LeftSide' }" v-tippy="{ content: tooltipText, maxWidth: 'none' }">
    {{ text }}
  </div>
</template>

<style scoped>
.hackToPreventSafariFromShowingTooltip::after {
  content: "";
  display: block;
}

.left-side-ellipsis {
  direction: rtl;
  text-align: left;
}

.text-container {
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  display: inline-block;
}
</style>
