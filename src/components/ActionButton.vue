<script setup lang="ts">
import FAIcon from "@/components/FAIcon.vue";
import { IconDefinition } from "@fortawesome/free-solid-svg-icons";

export type ButtonVariant = "primary" | "secondary" | "danger" | "link" | "default";
export type ButtonSize = "sm" | "lg" | "default";

interface Props {
  variant?: ButtonVariant;
  size?: ButtonSize;
  icon?: IconDefinition;
  iconPosition?: "left" | "right";
  disabled?: boolean;
  loading?: boolean;
  tooltip?: string;
  ariaLabel?: string;
  type?: "button" | "submit" | "reset";
}

const props = withDefaults(defineProps<Props>(), {
  variant: "default",
  size: "default",
  iconPosition: "left",
  disabled: false,
  loading: false,
  type: "button",
});

const variantClasses = {
  primary: "btn-primary",
  secondary: "btn-secondary",
  danger: "btn-danger",
  link: "btn-link",
  default: "btn-default",
};

const sizeClasses = {
  sm: "btn-sm",
  lg: "btn-lg",
  default: "",
};

const buttonClasses = ["btn", variantClasses[props.variant], sizeClasses[props.size]].filter(Boolean).join(" ");
</script>

<template>
  <button :class="[buttonClasses, { disabled: disabled || loading }]" :disabled="disabled || loading" :type="type" :aria-label="ariaLabel" v-tippy="tooltip">
    <FAIcon v-if="icon && iconPosition === 'left' && !loading" :icon="icon" class="icon-left" />

    <i v-if="loading" class="glyphicon glyphicon-refresh rotate"></i>

    <span v-if="$slots.default" class="button-text">
      <slot />
    </span>

    <FAIcon v-if="icon && iconPosition === 'right' && !loading" :icon="icon" class="icon-right" />
  </button>
</template>

<style scoped>
.btn {
  display: inline-flex;
  align-items: center;
  gap: 0.375rem;
  cursor: pointer;
}

.btn.disabled {
  cursor: not-allowed;
  opacity: 0.65;
}

.icon-left,
.icon-right {
  color: var(--reduced-emphasis);
}

.icon-left {
  margin-right: 0.25rem;
}

.icon-right {
  margin-left: 0.25rem;
}

.rotate {
  animation: spin 1s linear infinite;
}

@keyframes spin {
  from {
    transform: rotate(0deg);
  }
  to {
    transform: rotate(360deg);
  }
}

.button-text {
  flex: 1;
}
</style>
