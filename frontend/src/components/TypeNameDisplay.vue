<script setup lang="ts">
import { computed } from "vue";
import TextEllipses from "@/components/TextEllipses.vue";
import { formatTypeName } from "@/composables/formatUtils";

interface Props {
  typeName: string;
  maxWidth?: string;
  ellipsesStyle?: "RightSide" | "LeftSide";
  showRawType?: boolean;
}

const props = withDefaults(defineProps<Props>(), {
  maxWidth: "auto",
  ellipsesStyle: "RightSide",
  showRawType: false,
});

const displayText = computed(() => {
  return props.showRawType ? props.typeName : formatTypeName(props.typeName);
});

const tooltipText = computed(() => {
  return props.showRawType ? formatTypeName(props.typeName) : props.typeName;
});
</script>

<template>
  <div class="type-name-display" :style="{ width: maxWidth }" :title="tooltipText">
    <TextEllipses :text="displayText" :ellipses-style="ellipsesStyle" />
  </div>
</template>

<style scoped>
.type-name-display {
  display: inline-block;
  max-width: 100%;
}
</style>
