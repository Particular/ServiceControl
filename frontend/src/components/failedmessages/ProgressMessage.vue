<script setup lang="ts">
import { faArrowRight, faCheck } from "@fortawesome/free-solid-svg-icons";
import FAIcon from "@/components/FAIcon.vue";

const props = defineProps<{
  "left-to-do": boolean;
  active: boolean;
  completed: boolean;
}>();
</script>

<template>
  <li class="progress-status" :class="props">
    <FAIcon v-if="active" :icon="faArrowRight" class="icon" />
    <FAIcon v-if="completed" :icon="faCheck" class="icon" />
    <div class="slot">
      <slot />
    </div>
  </li>
</template>

<style scoped>
.progress-status {
  display: flex;
  align-items: center;
  margin-bottom: 6px;
}

.slot {
  flex: 1;
}

.progress-status.active {
  color: #fff;
}

.progress-status.leftToDo,
.progress-status.completed {
  color: #b0b5b5;
}

.progress-status.leftToDo {
  margin-left: 15px;
}

.icon {
  margin-right: 3px;
}
</style>
