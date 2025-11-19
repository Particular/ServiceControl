<script setup lang="ts">
import TypeNameDisplay from "@/components/TypeNameDisplay.vue";

interface Props {
  sagaType: string;
  maxWidth?: string;
  ellipsesStyle?: "RightSide" | "LeftSide";
  showTitle?: boolean;
  titleLevel?: "h1" | "h2" | "h3" | "h4" | "h5" | "h6" | "div";
  cssClass?: string;
}

withDefaults(defineProps<Props>(), {
  maxWidth: "182px",
  ellipsesStyle: "LeftSide",
  showTitle: false,
  titleLevel: "div",
  cssClass: "sagaName",
});
</script>

<template>
  <component :is="showTitle ? titleLevel : 'div'" :class="cssClass" :aria-label="showTitle ? 'saga name' : undefined">
    <TypeNameDisplay :type-name="sagaType" :max-width="maxWidth" :ellipses-style="ellipsesStyle" />
  </component>
</template>

<style scoped>
.sagaName {
  color: #e6e6e6;
}
</style>
