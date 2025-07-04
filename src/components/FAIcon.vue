<script setup lang="ts">
import { IconDefinition } from "@fortawesome/free-solid-svg-icons";
import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";

withDefaults(
  defineProps<{
    icon: IconDefinition;
    title?: string;
    size?: "2xs" | "xs" | "sm" | "lg" | "xl" | "2xl" | "1x" | "2x" | "3x" | "4x" | "5x" | "6x" | "7x" | "8x" | "9x" | "10x";
  }>(),
  { size: "1x" }
);
</script>

<template>
  <FontAwesomeIcon :icon="icon" :size="size" :title="title" />
</template>
