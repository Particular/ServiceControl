<script setup lang="ts">
import FAIcon from "@/components/FAIcon.vue";
import { IconDefinition, faCheck, faTimes, faExclamationTriangle, faInfoCircle } from "@fortawesome/free-solid-svg-icons";

export type StatusType = "success" | "error" | "warning" | "info";

interface Props {
  status: StatusType;
  message?: string;
  icon?: IconDefinition;
  size?: "2xs" | "xs" | "sm" | "lg" | "xl" | "2xl" | "1x" | "2x" | "3x" | "4x" | "5x" | "6x" | "7x" | "8x" | "9x" | "10x";
  showMessage?: boolean;
  customClass?: string;
}

const props = withDefaults(defineProps<Props>(), {
  message: "",
  size: "1x",
  showMessage: true,
  customClass: "",
});

const statusConfig = {
  success: {
    icon: faCheck,
    class: "text-success",
    defaultMessage: "Success",
  },
  error: {
    icon: faTimes,
    class: "text-danger",
    defaultMessage: "Error",
  },
  warning: {
    icon: faExclamationTriangle,
    class: "text-warning",
    defaultMessage: "Warning",
  },
  info: {
    icon: faInfoCircle,
    class: "text-info",
    defaultMessage: "Info",
  },
};

const currentConfig = statusConfig[props.status];
const displayIcon = props.icon || currentConfig.icon;
const displayMessage = props.message || currentConfig.defaultMessage;
const cssClass = props.customClass || currentConfig.class;
</script>

<template>
  <span :class="['status-icon', cssClass]">
    <FAIcon :icon="displayIcon" :size="size" :title="showMessage ? displayMessage : undefined" />
    <span v-if="showMessage && message" class="status-message">{{ displayMessage }}</span>
  </span>
</template>

<style scoped>
.status-icon {
  display: inline-flex;
  align-items: center;
  gap: 0.25rem;
}

.text-success {
  color: #28a745;
}

.text-danger {
  color: #dc3545;
}

.text-warning {
  color: #ffc107;
}

.text-info {
  color: #17a2b8;
}

.info-color {
  color: #17a2b8;
}

.error-color {
  color: #dc3545;
}

.status-message {
  margin-left: 0.25rem;
}
</style>
