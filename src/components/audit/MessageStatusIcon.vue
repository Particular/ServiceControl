<script setup lang="ts">
import { dotNetTimespanToMilliseconds } from "@/composables/formatUtils";
import Message, { MessageStatus } from "@/resources/Message";
import { defineProps, computed } from "vue";

const props = defineProps<{
  message: Message;
}>();

const hasWarning = computed(() => {
  return (
    props.message.status === MessageStatus.ResolvedSuccessfully ||
    dotNetTimespanToMilliseconds(props.message.critical_time) < 0 ||
    dotNetTimespanToMilliseconds(props.message.processing_time) < 0 ||
    dotNetTimespanToMilliseconds(props.message.delivery_time) < 0
  );
});

const statusInfo = computed(() => {
  switch (props.message.status) {
    case MessageStatus.Successful:
      return { name: "Successful", icon: "fa successful" };
    case MessageStatus.ResolvedSuccessfully:
      return { name: "Successful after retries", icon: "fa resolved-successfully" };
    case MessageStatus.Failed:
      return { name: "Failed", icon: "fa failed" };
    case MessageStatus.ArchivedFailure:
      return { name: "Failed message deleted", icon: "fa archived" };
    case MessageStatus.RepeatedFailure:
      return { name: "Repeated failures", icon: "fa repeated-failure" };
    case MessageStatus.RetryIssued:
      return { name: "Retry requested", icon: "fa retry-issued" };
    default:
      return { name: "Unknown status", icon: "fa unknown-status" };
  }
});
</script>

<template>
  <div class="status-container" v-tippy="{ content: statusInfo.name }">
    <div class="status-icon" :class="statusInfo.icon"></div>
    <div v-if="hasWarning" class="warning"></div>
  </div>
</template>

<style scoped>
.status-container {
  color: white;
  width: 1.4em;
  height: 1.4em;
  position: relative;
}

.status-icon {
  background-position: center;
  background-repeat: no-repeat;
  height: 1.4em;
  width: 1.4em;
}

.warning {
  background-image: url("@/assets/warning.svg");
  background-position: bottom;
  background-repeat: no-repeat;
  height: 0.93em;
  width: 0.93em;
  position: absolute;
  right: 0;
  bottom: 0;
}

.successful {
  background-image: url("@/assets/status_successful.svg");
}

.resolved-successfully {
  background-image: url("@/assets/status_resolved.svg");
}

.failed {
  background-image: url("@/assets/status_failed.svg");
}

.archived {
  background-image: url("@/assets/status_archived.svg");
}

.repeated-failure {
  background-image: url("@/assets/status_repeated_failed.svg");
}

.retry-issued {
  background-image: url("@/assets/status_retry_issued.svg");
}

.unknown-status::after {
  content: "?";
  color: #fff;
  font-weight: bold;
  border-radius: 50%;
  width: 1.4em;
  line-height: 1.4em;
  background-color: var(--reduced-emphasis);
  display: flex;
  align-items: center;
  justify-content: center;
}
</style>
